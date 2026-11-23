namespace Localizer.Infrastructure.Persistence.Json;

public sealed class CatalogDocumentDto
{
    public int SchemaVersion { get; set; }

    public string CatalogId { get; set; } = string.Empty;

    public string SourceLocale { get; set; } = string.Empty;

    public List<string> RequiredLocales { get; set; } = [];

    public string DefaultSyntaxProfile { get; set; } = string.Empty;

    public string? ProjectContext { get; set; }

    public string? Glossary { get; set; }

    public List<CatalogEntryDto> Entries { get; set; } = [];
}
