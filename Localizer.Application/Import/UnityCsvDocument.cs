namespace Localizer.Application.Import;

public sealed record UnityCsvRow
{
    public required string Key { get; init; }

    public required string UnityId { get; init; }

    public required string SourceText { get; init; }
}

public sealed record UnityCsvDocument
{
    public required string SourceLocale { get; init; }

    public required IReadOnlyList<string> TargetLocales { get; init; }

    public required IReadOnlyList<UnityCsvRow> Rows { get; init; }
}
