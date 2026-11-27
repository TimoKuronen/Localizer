using Localizer.Core.Lifecycle;

namespace Localizer.Application.UseCases;

/// <summary>
/// A catalog entry whose key a source file no longer lists.
/// </summary>
public sealed record SourceMissingEntry
{
    public required string Key { get; init; }

    public required string SourceText { get; init; }

    /// <summary>
    /// Effective status per required locale, in locale order.
    /// </summary>
    public required IReadOnlyList<(string Locale, TranslationEffectiveStatus Status)> LocaleStatuses { get; init; }

    /// <summary>
    /// True when any translation was approved, including one that has since become Stale.
    /// </summary>
    public required bool HasApprovedText { get; init; }
}
