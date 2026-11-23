using System.Text.Json;
using Localizer.Application.Drafting;

namespace Localizer.Infrastructure.Drafting;

internal static class OllamaDraftResponseParser
{
    private const int MaxLengthRatio = 3;
    private const int MaxLengthSlack = 40;

    private static readonly string[] EchoMarkers =
    [
        OllamaDraftPromptBuilder.SourceStartMarker,
        OllamaDraftPromptBuilder.SourceEndMarker,
        "Developer notes",
        "Context:",
        "Syntax profile"
    ];

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
}
