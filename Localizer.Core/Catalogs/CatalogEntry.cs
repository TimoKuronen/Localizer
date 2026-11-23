using Localizer.Core.Lifecycle;

namespace Localizer.Core.Catalogs;

public sealed record CatalogEntry
{
    public required Identity.EntryKey Key { get; init; }
    public string? Category { get; init; }
    public required string SourceText { get; init; }
    public bool AllowEmptyText { get; init; }
    public string? DeveloperNotes { get; init; }
    public TranslationContext Context { get; init; } = new();
    public EntryConstraints Constraints { get; init; } = new();
    public Syntax.MessageSyntaxProfile? SyntaxProfileOverride { get; init; }
    public ExternalIds ExternalIds { get; init; } = new();
    public IReadOnlyDictionary<Identity.Locale, Translation> Translations { get; init; } =
        new Dictionary<Identity.Locale, Translation>();
}
