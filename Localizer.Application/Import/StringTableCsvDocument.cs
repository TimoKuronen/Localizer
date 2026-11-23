namespace Localizer.Application.Import;

public sealed record StringTableCsvRow
{
    public required string Key { get; init; }

    public required string ExternalId { get; init; }

    public required string SourceText { get; init; }
}

public sealed record StringTableCsvDocument
{
    public required string ExternalIdNamespace { get; init; }

    public required string SourceLocale { get; init; }

    public required IReadOnlyList<string> TargetLocales { get; init; }

    public required IReadOnlyList<StringTableCsvRow> Rows { get; init; }
}
