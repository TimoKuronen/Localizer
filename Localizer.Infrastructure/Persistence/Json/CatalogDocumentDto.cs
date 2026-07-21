namespace Localizer.Infrastructure.Persistence.Json;

public sealed class CatalogDocumentDto
{
    public int SchemaVersion { get; set; }

    public string CatalogId { get; set; } = string.Empty;

    public string SourceLocale { get; set; } = string.Empty;

    public List<string> RequiredLocales { get; set; } = [];

    public string DefaultSyntaxProfile { get; set; } = string.Empty;

    public List<CatalogEntryDto> Entries { get; set; } = [];
}

public sealed class CatalogEntryDto
{
    public string Key { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string SourceText { get; set; } = string.Empty;

    public bool AllowEmptyText { get; set; }

    public string? DeveloperNotes { get; set; }

    public Dictionary<string, string>? Context { get; set; }

    public EntryConstraintsDto? Constraints { get; set; }

    public string? SyntaxProfile { get; set; }

    public Dictionary<string, string>? ExternalIds { get; set; }

    public Dictionary<string, TranslationDto>? Translations { get; set; }
}

public sealed class EntryConstraintsDto
{
    public int? MaxGraphemes { get; set; }

    public int? MaxUtf8Bytes { get; set; }

    public int? MaxLines { get; set; }

    public List<string>? RequiredTerms { get; set; }

    public List<string>? ForbiddenTerms { get; set; }
}

public sealed class TranslationDto
{
    public string Text { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string BasedOnFingerprint { get; set; } = string.Empty;

    public string Origin { get; set; } = string.Empty;

    public TranslationProvenanceDto? Provenance { get; set; }
}

public sealed class TranslationProvenanceDto
{
    public string? ProviderName { get; set; }

    public string? ModelName { get; set; }

    public DateTimeOffset? GeneratedAtUtc { get; set; }
}
