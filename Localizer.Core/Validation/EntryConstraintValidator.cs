// Checks configured length, line, and term restrictions against a piece of text.
// Each constraint uses its own measuring rule so UI and file-size limits stay accurate.
namespace Localizer.Core.Validation;

public static class EntryConstraintValidator
{
    public static IReadOnlyList<ValidationDiagnostic> Validate(
        string text,
        Catalogs.EntryConstraints constraints,
        Identity.EntryKey entryKey,
        Identity.Locale? locale)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        if (constraints.MaxGraphemes is int maxGraphemes)
        {
            var graphemeCount = CountGraphemes(text);
            if (graphemeCount > maxGraphemes)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    DiagnosticCodes.ConstraintGraphemeExceeded,
                    ValidationSeverity.Error,
                    $"Text contains {graphemeCount} graphemes but the limit is {maxGraphemes}.",
                    entryKey,
                    locale));
            }
        }

        if (constraints.MaxUtf8Bytes is int maxUtf8Bytes)
        {
            var byteCount = System.Text.Encoding.UTF8.GetByteCount(text);
            if (byteCount > maxUtf8Bytes)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    DiagnosticCodes.ConstraintUtf8BytesExceeded,
                    ValidationSeverity.Error,
                    $"Text uses {byteCount} UTF-8 bytes but the limit is {maxUtf8Bytes}.",
                    entryKey,
                    locale));
            }
        }

        if (constraints.MaxLines is int maxLines)
        {
            var lineCount = CountLines(text);
            if (lineCount > maxLines)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    DiagnosticCodes.ConstraintLinesExceeded,
                    ValidationSeverity.Error,
                    $"Text spans {lineCount} lines but the limit is {maxLines}.",
                    entryKey,
                    locale));
            }
        }

        foreach (var requiredTerm in constraints.RequiredTerms)
        {
            if (string.IsNullOrEmpty(requiredTerm))
            {
                continue;
            }

            if (!text.Contains(requiredTerm, StringComparison.Ordinal))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    DiagnosticCodes.ConstraintTermRequiredMissing,
                    ValidationSeverity.Error,
                    $"Text must contain required term '{requiredTerm}'.",
                    entryKey,
                    locale));
            }
        }

        foreach (var forbiddenTerm in constraints.ForbiddenTerms)
        {
            if (string.IsNullOrEmpty(forbiddenTerm))
            {
                continue;
            }

            if (text.Contains(forbiddenTerm, StringComparison.Ordinal))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    DiagnosticCodes.ConstraintTermForbiddenPresent,
                    ValidationSeverity.Error,
                    $"Text must not contain forbidden term '{forbiddenTerm}'.",
                    entryKey,
                    locale));
            }
        }

        return diagnostics;
    }

    internal static int CountGraphemes(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var elementEnumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        var count = 0;
        while (elementEnumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    internal static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 1;
        }

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return normalized.Split('\n').Length;
    }
}
