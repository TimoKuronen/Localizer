using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Localizer.Application.Drafting;

namespace Localizer.Infrastructure.Drafting;

public sealed class OllamaTranslationDraftProvider : ITranslationDraftProvider
{
    public const string ProviderName = "ollama";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly OllamaDraftProviderOptions _options;

    public OllamaTranslationDraftProvider(HttpClient httpClient, OllamaDraftProviderOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.ModelName))
        {
            throw new ArgumentException("Model name is required.", nameof(options));
        }
    }

    public async Task<TranslationDraftBatchResult> DraftAsync(
        TranslationDraftBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var installedModels = await ListInstalledModelsAsync(cancellationToken).ConfigureAwait(false);
        var modelName = ResolveModelName(_options.ModelName, installedModels);

        var results = new List<TranslationDraftItemResult>(request.Items.Count);
        var failures = new List<string>();

        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var drafted = await DraftOneAsync(item, modelName, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
    {
        var payload = new OllamaChatRequest
        {
            Model = modelName,
            Stream = false,
            Format = "json",
            Think = false,
            Messages =
            [
                new OllamaChatMessage
                {
                    Role = "system",
                    Content = BuildSystemPrompt()
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

    private async Task<IReadOnlyList<string>> ListInstalledModelsAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("api/tags", cancellationToken).ConfigureAwait(false);
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
            if (model.TryGetProperty("name", out var nameElement)
                && nameElement.ValueKind == JsonValueKind.String
                && nameElement.GetString() is { Length: > 0 } name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    internal static string ResolveModelName(string preferredModel, IReadOnlyList<string> installedModels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredModel);
        ArgumentNullException.ThrowIfNull(installedModels);

        foreach (var installed in installedModels)
        {
            if (ModelMatches(installed, preferredModel))
            {
                return installed;
            }
        }

        if (installedModels.Count == 1)
        {
            return installedModels[0];
        }

        if (installedModels.Count == 0)
        {
            throw new InvalidOperationException(
                $"No Ollama models are installed. Pull a model or set LOCALIZER_OLLAMA_MODEL. Preferred model was '{preferredModel}'.");
        }

        throw new InvalidOperationException(
            $"Ollama model '{preferredModel}' was not found. Available: {string.Join(", ", installedModels)}. Set LOCALIZER_OLLAMA_MODEL to one of these.");
    }

    internal static bool ModelMatches(string installedModel, string requestedModel)
    {
        if (string.Equals(installedModel, requestedModel, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(installedModel, requestedModel + ":latest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var installedBase = installedModel.Split(':', 2)[0];
        var requestedBase = requestedModel.Split(':', 2)[0];
        return string.Equals(installedBase, requestedBase, StringComparison.OrdinalIgnoreCase);
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
        builder.AppendLine("Source text:");
        builder.AppendLine(item.SourceText);

        if (!string.IsNullOrWhiteSpace(item.DeveloperNotes))
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

    private static string BuildSystemPrompt() =>
        """
        You are a careful localization translator.
        Preserve placeholders exactly when the syntax profile is composite (for example {0}, {1}, {{, }}).
        Do not add commentary.
        Return a single JSON object with keys key, locale, and text.
        """;

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

        [JsonPropertyName("messages")]
        public required IReadOnlyList<OllamaChatMessage> Messages { get; init; }
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
