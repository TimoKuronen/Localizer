using Localizer.Core.Lifecycle;

namespace Localizer.Core.Catalogs;

public sealed record TranslationProvenance
{
    public required TranslationOrigin Origin { get; init; }
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public DateTimeOffset? GeneratedAtUtc { get; init; }
}

public sealed record Translation
{
    public required string Text { get; init; }
    public required TranslationState State { get; init; }
    public required Fingerprints.Fingerprint BasedOnFingerprint { get; init; }
    public required TranslationProvenance Provenance { get; init; }
}

public sealed record EntryConstraints
{
    public int? MaxGraphemes { get; init; }
    public int? MaxUtf8Bytes { get; init; }
    public int? MaxLines { get; init; }
    public IReadOnlyList<string> RequiredTerms { get; init; } = [];
    public IReadOnlyList<string> ForbiddenTerms { get; init; } = [];
}

public sealed record TranslationContext
{
    public IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record ExternalIds
{
    public IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

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
