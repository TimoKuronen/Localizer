namespace Localizer.Application.Drafting;

public sealed record TranslationDraftItemRequest
{
    public required string Key { get; init; }

    public required string TargetLocale { get; init; }

    public required string SourceLocale { get; init; }

    public required string SourceText { get; init; }

    public string? DeveloperNotes { get; init; }

    public IReadOnlyDictionary<string, string> Context { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public DraftConstraintHints Constraints { get; init; } = new();

    public required string SyntaxProfile { get; init; }
}
