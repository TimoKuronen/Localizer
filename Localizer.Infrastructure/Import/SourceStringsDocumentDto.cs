using Localizer.Infrastructure.Persistence.Json;

namespace Localizer.Infrastructure.Import;

internal sealed class SourceStringsDocumentDto
{
    public int SchemaVersion { get; set; }

    public string? SourceLocale { get; set; }

    public List<SourceStringEntryDto>? Entries { get; set; }
}

internal sealed class SourceStringEntryDto
{
    public string? Key { get; set; }

    public string? SourceText { get; set; }

    public string? Category { get; set; }

    public bool? AllowEmptyText { get; set; }

    public string? DeveloperNotes { get; set; }

    public Dictionary<string, string>? Context { get; set; }

    public EntryConstraintsDto? Constraints { get; set; }

    public string? SyntaxProfile { get; set; }

    public Dictionary<string, string>? ExternalIds { get; set; }
}
