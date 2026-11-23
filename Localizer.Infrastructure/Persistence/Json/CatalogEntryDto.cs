namespace Localizer.Infrastructure.Persistence.Json;

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
