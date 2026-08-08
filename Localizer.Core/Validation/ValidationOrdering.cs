// Sorts validation findings into a stable order for deterministic tests and UI lists.
// Ordering is entry key, then locale, then code, then text position.
namespace Localizer.Core.Validation;

public static class ValidationOrdering
{
    public static IReadOnlyList<ValidationDiagnostic> Sort(IEnumerable<ValidationDiagnostic> diagnostics) =>
        diagnostics
            .OrderBy(diagnostic => diagnostic.EntryKey?.Value ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Locale?.Value ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Location?.StartOffset ?? int.MaxValue)
            .ToList();
}
