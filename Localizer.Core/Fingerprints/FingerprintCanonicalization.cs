using System.Text;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;

namespace Localizer.Core.Fingerprints;

public static class FingerprintCanonicalization
{
    public const int ContractVersion = 1;

    public static string NormalizeNewlines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    public static string BuildCanonicalInput(
        Locale sourceLocale,
        string sourceText,
        MessageSyntaxProfile syntaxProfile,
        string? developerNotes,
        TranslationContext context,
        EntryConstraints constraints)
    {
        var builder = new StringBuilder();
        AppendLine(builder, "fingerprintVersion", ContractVersion.ToString());
        AppendLine(builder, "sourceLocale", sourceLocale.Value);
        AppendLine(builder, "syntaxProfile", MessageSyntaxProfileNames.ToName(syntaxProfile));
        AppendSection(builder, "sourceText", NormalizeNewlines(sourceText));
        AppendSection(builder, "developerNotes", NormalizeNewlines(developerNotes ?? string.Empty));

        AppendMapSection(builder, "context", context.Values);
        AppendConstraintsSection(builder, constraints);

        return builder.ToString();
    }

    public static Fingerprint Compute(
        Locale sourceLocale,
        string sourceText,
        MessageSyntaxProfile syntaxProfile,
        string? developerNotes,
        TranslationContext context,
        EntryConstraints constraints)
    {
        var canonical = BuildCanonicalInput(
            sourceLocale,
            sourceText,
            syntaxProfile,
            developerNotes,
            context,
            constraints);

        return Fingerprint.Create(canonical);
    }

    public static Fingerprint ComputeForEntry(CatalogEntry entry, Locale sourceLocale, MessageSyntaxProfile defaultSyntaxProfile)
    {
        var syntaxProfile = entry.SyntaxProfileOverride ?? defaultSyntaxProfile;
        return Compute(
            sourceLocale,
            entry.SourceText,
            syntaxProfile,
            entry.DeveloperNotes,
            entry.Context,
            entry.Constraints);
    }

    private static void AppendLine(StringBuilder builder, string key, string value)
    {
        builder.Append(key);
        builder.Append('=');
        builder.Append(value);
        builder.Append('\n');
    }

    private static void AppendSection(StringBuilder builder, string sectionName, string value)
    {
        builder.Append('[');
        builder.Append(sectionName);
        builder.Append("]\n");
        builder.Append(value);
        if (!value.EndsWith('\n'))
        {
            builder.Append('\n');
        }

        builder.Append("[/");
        builder.Append(sectionName);
        builder.Append("]\n");
    }

    private static void AppendMapSection(StringBuilder builder, string sectionName, IReadOnlyDictionary<string, string> values)
    {
        builder.Append('[');
        builder.Append(sectionName);
        builder.Append("]\n");

        foreach (var pair in values.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key);
            builder.Append('=');
            builder.Append(NormalizeNewlines(pair.Value));
            builder.Append('\n');
        }

        builder.Append("[/");
        builder.Append(sectionName);
        builder.Append("]\n");
    }

    private static void AppendConstraintsSection(StringBuilder builder, EntryConstraints constraints)
    {
        builder.Append("[constraints]\n");

        if (constraints.MaxGraphemes is not null)
        {
            builder.Append("maxGraphemes=");
            builder.Append(constraints.MaxGraphemes.Value);
            builder.Append('\n');
        }

        if (constraints.MaxUtf8Bytes is not null)
        {
            builder.Append("maxUtf8Bytes=");
            builder.Append(constraints.MaxUtf8Bytes.Value);
            builder.Append('\n');
        }

        if (constraints.MaxLines is not null)
        {
            builder.Append("maxLines=");
            builder.Append(constraints.MaxLines.Value);
            builder.Append('\n');
        }

        foreach (var term in constraints.RequiredTerms.OrderBy(static term => term, StringComparer.Ordinal))
        {
            builder.Append("requiredTerm=");
            builder.Append(NormalizeNewlines(term));
            builder.Append('\n');
        }

        foreach (var term in constraints.ForbiddenTerms.OrderBy(static term => term, StringComparer.Ordinal))
        {
            builder.Append("forbiddenTerm=");
            builder.Append(NormalizeNewlines(term));
            builder.Append('\n');
        }

        builder.Append("[/constraints]\n");
    }
}
