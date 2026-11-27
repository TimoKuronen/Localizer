using Localizer.Core.Lifecycle;

namespace Localizer.Application.UseCases;

/// <summary>
/// Another entry with the same source whose translation for one locale is not currently Approved.
/// </summary>
public sealed record MatchingTranslation
{
    public required string Key { get; init; }

    public required TranslationEffectiveStatus Status { get; init; }

    public string? CurrentText { get; init; }

    public string? DeveloperNotes { get; init; }

    /// <summary>
    /// True when syntax, developer notes, context and constraints also match.
    /// </summary>
    public required bool IsIdentical { get; init; }

    /// <summary>
    /// True when the stored text was approved before it became Stale.
    /// </summary>
    public required bool WasApproved { get; init; }
}
