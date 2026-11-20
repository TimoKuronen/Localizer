using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Localizer.Application.Drafting;

namespace Localizer.Infrastructure.Drafting;

public sealed class OllamaTranslationDraftProvider : ITranslationDraftProvider, IDraftModelCatalog
{
    public const string ProviderName = "ollama";

    private const string SourceStartMarker = "<<<";
    private const string SourceEndMarker = ">>>";
    private const int MaxLengthRatio = 3;
    private const int MaxLengthSlack = 40;

    private static readonly string[] EchoMarkers =
    [
        SourceStartMarker,
        SourceEndMarker,
        "Developer notes",
        "Context:",
        "Syntax profile"
    ];

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

        var systemPrompt = BuildSystemPrompt(request.ProjectContext, request.Glossary);
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
                    Content = BuildUserPrompt(item)
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

        return ParseDraftContent(item, content);
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

    internal static TranslationDraftItemResult ParseDraftContent(
        TranslationDraftItemRequest item,
        string content)
    {
        using var document = JsonDocument.Parse(ExtractJsonObject(content));
        var root = document.RootElement;

        if (!root.TryGetProperty("text", out var textElement) || textElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                $"Ollama JSON for '{item.Key}' ({item.TargetLocale}) did not contain a string 'text' field.");
        }

        var text = textElement.GetString() ?? string.Empty;
        RejectEchoedReferenceText(item, text);

        if (root.TryGetProperty("key", out var keyElement)
            && keyElement.ValueKind == JsonValueKind.String
            && keyElement.GetString() is { } returnedKey
            && !string.Equals(returnedKey, item.Key, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Ollama returned key '{returnedKey}' but '{item.Key}' was requested.");
        }

        if (root.TryGetProperty("locale", out var localeElement)
            && localeElement.ValueKind == JsonValueKind.String
            && localeElement.GetString() is { } returnedLocale
            && !string.Equals(returnedLocale, item.TargetLocale, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Ollama returned locale '{returnedLocale}' but '{item.TargetLocale}' was requested.");
        }

        return new TranslationDraftItemResult
        {
            Key = item.Key,
            TargetLocale = item.TargetLocale,
            Text = text
        };
    }

    internal static string BuildUserPrompt(TranslationDraftItemRequest item)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Translate from {item.SourceLocale} to {item.TargetLocale}.");
        builder.AppendLine($"Key: {item.Key}");
        builder.AppendLine($"Syntax profile: {item.SyntaxProfile}");
        builder.AppendLine("Source text (translate only what is between the markers):");
        builder.AppendLine(SourceStartMarker);
        builder.AppendLine(item.SourceText);
        builder.AppendLine(SourceEndMarker);

        var hasNotes = !string.IsNullOrWhiteSpace(item.DeveloperNotes);
        if (hasNotes || item.Context.Count > 0)
        {
            builder.AppendLine("The notes below are reference only. Never translate, repeat or paraphrase them.");
        }

        if (hasNotes)
        {
            builder.AppendLine("Developer notes:");
            builder.AppendLine(item.DeveloperNotes);
        }

        if (item.Context.Count > 0)
        {
            builder.AppendLine("Context:");
            foreach (var pair in item.Context.OrderBy(static p => p.Key, StringComparer.Ordinal))
            {
                builder.AppendLine($"- {pair.Key}: {pair.Value}");
            }
        }

        AppendConstraints(builder, item.Constraints);
        builder.AppendLine("Respond with JSON only using this shape:");
        builder.AppendLine(
            $"{{\"key\":\"{item.Key}\",\"locale\":\"{item.TargetLocale}\",\"text\":\"...\"}}");
        return builder.ToString();
    }

    internal static string BuildSystemPrompt(string? projectContext, string? glossary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are a careful localization translator for software user interfaces.");
        builder.AppendLine(
            "Translate only the text between the " + SourceStartMarker + " and " + SourceEndMarker
            + " markers. Output the translation of that text and nothing else.");
        builder.AppendLine("Never include developer notes, context, constraints, explanations or the markers in the output.");
        builder.AppendLine(
            "Translate every word into the target language. Keep only placeholders, markup, product names and glossary terms unchanged.");
        builder.AppendLine("Use natural, idiomatic wording that fits a user interface, not a word-for-word rendering.");
        builder.AppendLine("Keep the same number of lines as the source text.");
        builder.AppendLine(
            "Preserve placeholders exactly when the syntax profile is composite (for example {0}, {1}, {{, }}).");
        builder.AppendLine("Do not add commentary.");
        builder.AppendLine("Return a single JSON object with keys key, locale, and text.");

        if (!string.IsNullOrWhiteSpace(projectContext))
        {
            builder.AppendLine();
            builder.AppendLine("About the product being translated:");
            builder.AppendLine(projectContext.Trim());
        }

        if (!string.IsNullOrWhiteSpace(glossary))
        {
            builder.AppendLine();
            builder.AppendLine(
                "Glossary. Use these translations for these terms. A line starting with a locale code and a colon applies only to that locale:");
            builder.AppendLine(glossary.Trim());
        }

        return builder.ToString();
    }

    private static void RejectEchoedReferenceText(TranslationDraftItemRequest item, string text)
    {
        foreach (var marker in EchoMarkers)
        {
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase)
                && !item.SourceText.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Draft for '{item.Key}' ({item.TargetLocale}) repeated prompt text ('{marker}').");
            }
        }

        if (CountLines(text) > CountLines(item.SourceText))
        {
            throw new InvalidOperationException(
                $"Draft for '{item.Key}' ({item.TargetLocale}) has more lines than the source text.");
        }

        if (text.Length > item.SourceText.Length * MaxLengthRatio + MaxLengthSlack)
        {
            throw new InvalidOperationException(
                $"Draft for '{item.Key}' ({item.TargetLocale}) is far longer than the source text.");
        }
    }

    private static int CountLines(string value) => value.Trim().Count(static c => c == '\n');

    private static void AppendConstraints(StringBuilder builder, DraftConstraintHints constraints)
    {
        var hasConstraints =
            constraints.MaxGraphemes is not null
            || constraints.MaxUtf8Bytes is not null
            || constraints.MaxLines is not null
            || constraints.RequiredTerms.Count > 0
            || constraints.ForbiddenTerms.Count > 0;

        if (!hasConstraints)
        {
            return;
        }

        builder.AppendLine("Constraints:");
        if (constraints.MaxGraphemes is int maxGraphemes)
        {
            builder.AppendLine($"- Max graphemes: {maxGraphemes}");
        }

        if (constraints.MaxUtf8Bytes is int maxUtf8Bytes)
        {
            builder.AppendLine($"- Max UTF-8 bytes: {maxUtf8Bytes}");
        }

        if (constraints.MaxLines is int maxLines)
        {
            builder.AppendLine($"- Max lines: {maxLines}");
        }

        if (constraints.RequiredTerms.Count > 0)
        {
            builder.AppendLine($"- Required terms: {string.Join(", ", constraints.RequiredTerms)}");
        }

        if (constraints.ForbiddenTerms.Count > 0)
        {
            builder.AppendLine($"- Forbidden terms: {string.Join(", ", constraints.ForbiddenTerms)}");
        }
    }

    private static string ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            throw new InvalidOperationException("Ollama response did not contain a JSON object.");
        }

        return content[start..(end + 1)];
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
