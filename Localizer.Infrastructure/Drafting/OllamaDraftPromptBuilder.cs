using System.Text;
using Localizer.Application.Drafting;

namespace Localizer.Infrastructure.Drafting;

internal static class OllamaDraftPromptBuilder
{
    public const string SourceStartMarker = "<<<";
    public const string SourceEndMarker = ">>>";

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
}
