using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Localizer.Application.Drafting;

namespace Localizer.Infrastructure.Drafting;

public sealed class OllamaTranslationDraftProvider : ITranslationDraftProvider, IDraftModelCatalog
{
    public const string ProviderName = "ollama";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly OllamaDraftProviderOptions _options;
    private readonly IDraftModelPreference? _preference;

    /// <param name="preference">
    /// Persisted model choice used when the options do not fix a model.
    /// </param>
    public OllamaTranslationDraftProvider(
        HttpClient httpClient,
        OllamaDraftProviderOptions options,
        IDraftModelPreference? preference = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _preference = preference;
    }

    public async Task<TranslationDraftBatchResult> DraftAsync(
        TranslationDraftBatchRequest request,
        IProgress<TranslationDraftProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var chatModels = await ListChatModelsAsync(cancellationToken).ConfigureAwait(false);
        var configuredModel = _options.ModelName;
        if (string.IsNullOrWhiteSpace(configuredModel) && _preference is not null)
        {
            configuredModel = await _preference.GetAsync(cancellationToken).ConfigureAwait(false);
        }

        var modelName = ResolveModelName(configuredModel, chatModels);

        var systemPrompt = OllamaDraftPromptBuilder.BuildSystemPrompt(request.ProjectContext, request.Glossary);
        var results = new List<TranslationDraftItemResult>(request.Items.Count);
        var failures = new List<string>();
        var total = request.Items.Count;
        var completed = 0;
        progress?.Report(new TranslationDraftProgress(completed, total));

        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var drafted = await DraftOneAsync(item, modelName, systemPrompt, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(drafted);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                failures.Add($"{item.Key} ({item.TargetLocale}): {ex.Message}");
            }
            catch (Exception ex)
            {
                failures.Add($"{item.Key} ({item.TargetLocale}): {ex.Message}");
            }

            completed++;
            progress?.Report(new TranslationDraftProgress(completed, total));
        }

        if (results.Count == 0 && request.Items.Count > 0)
        {
            throw new InvalidOperationException(
                "All draft requests failed. " + string.Join(" ", failures));
        }

        return new TranslationDraftBatchResult
        {
            ProviderName = ProviderName,
            ModelName = modelName,
            Items = results,
            ItemFailures = failures
        };
    }

    private async Task<TranslationDraftItemResult> DraftOneAsync(
        TranslationDraftItemRequest item,
        string modelName,
        string systemPrompt,
        CancellationToken cancellationToken)
    {
        var payload = new OllamaChatRequest
        {
            Model = modelName,
            Stream = false,
            Format = "json",
            Think = false,
            Options = new OllamaChatOptions
            {
                Temperature = _options.Temperature,
                NumCtx = _options.ContextTokens
            },
            Messages =
            [
                new OllamaChatMessage
                {
                    Role = "system",
                    Content = systemPrompt
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = OllamaDraftPromptBuilder.BuildUserPrompt(item)
                }
            ]
        };

        using var response = await _httpClient
            .PostAsJsonAsync("api/chat", payload, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Ollama returned {(int)response.StatusCode}: {Truncate(body, 240)}");
        }

        var chat = await response.Content
            .ReadFromJsonAsync<OllamaChatResponse>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        var content = chat?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Ollama returned an empty message.");
        }

        return OllamaDraftResponseParser.ParseDraftContent(item, content);
    }

    public async Task<IReadOnlyList<string>> ListChatModelsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await GetModelListAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Ollama model list failed with {(int)response.StatusCode}: {Truncate(body, 240)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("models", out var modelsElement)
            || modelsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var names = new List<string>();
        foreach (var model in modelsElement.EnumerateArray())
        {
            if (SupportsChat(model)
                && model.TryGetProperty("name", out var nameElement)
                && nameElement.ValueKind == JsonValueKind.String
                && nameElement.GetString() is { Length: > 0 } name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    private async Task<HttpResponseMessage> GetModelListAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.GetAsync("api/tags", cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach Ollama at {_options.BaseAddress}. Start Ollama or set LOCALIZER_OLLAMA_URL.",
                ex);
        }
    }

    // Ollama versions that omit capabilities keep every model eligible.
    private static bool SupportsChat(JsonElement model)
    {
        if (!model.TryGetProperty("capabilities", out var capabilities)
            || capabilities.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        return capabilities.EnumerateArray().Any(static capability =>
            capability.ValueKind == JsonValueKind.String && capability.GetString() == "completion");
    }

    internal static string ResolveModelName(string? configuredModel, IReadOnlyList<string> chatModels)
    {
        ArgumentNullException.ThrowIfNull(chatModels);

        if (chatModels.Count == 0)
        {
            throw new InvalidOperationException(
                "No chat-capable Ollama models are installed. Pull one with 'ollama pull <model>'.");
        }

        var available = string.Join(", ", chatModels);

        if (string.IsNullOrWhiteSpace(configuredModel))
        {
            return chatModels.Count == 1
                ? chatModels[0]
                : throw new InvalidOperationException(
                    $"Several chat-capable Ollama models are installed: {available}. Choose one in the app or set LOCALIZER_OLLAMA_MODEL.");
        }

        var matches = chatModels.Where(model => ModelMatches(model, configuredModel)).ToList();
        var exact = matches.FirstOrDefault(model =>
            string.Equals(model, configuredModel, StringComparison.OrdinalIgnoreCase)
            || string.Equals(model, configuredModel + ":latest", StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact;
        }

        return matches.Count == 1
            ? matches[0]
            : throw new InvalidOperationException(
                $"Ollama model '{configuredModel}' does not match a single chat-capable model. Available: {available}. Choose one in the app or set LOCALIZER_OLLAMA_MODEL.");
    }

    internal static bool ModelMatches(string installedModel, string requestedModel)
    {
        if (string.Equals(installedModel, requestedModel, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (requestedModel.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var installedBase = installedModel.Split(':', 2)[0];
        return string.Equals(installedBase, requestedModel, StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }

        [JsonPropertyName("format")]
        public string? Format { get; init; }

        [JsonPropertyName("think")]
        public bool Think { get; init; }

        [JsonPropertyName("options")]
        public required OllamaChatOptions Options { get; init; }

        [JsonPropertyName("messages")]
        public required IReadOnlyList<OllamaChatMessage> Messages { get; init; }
    }

    private sealed class OllamaChatOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; init; }
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaChatMessage? Message { get; init; }
    }
}
