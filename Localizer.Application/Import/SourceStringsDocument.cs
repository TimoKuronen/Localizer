using Localizer.Core.Catalogs;
using Localizer.Core.Syntax;

namespace Localizer.Application.Import;

public sealed record SourceStringRow
{
    public required string Key { get; init; }

    public required string SourceText { get; init; }

    public string? Category { get; init; }

    public bool? AllowEmptyText { get; init; }

    public string? DeveloperNotes { get; init; }

    public IReadOnlyDictionary<string, string>? Context { get; init; }

    public EntryConstraints? Constraints { get; init; }

    public MessageSyntaxProfile? SyntaxProfile { get; init; }

    public IReadOnlyDictionary<string, string>? ExternalIds { get; init; }
}

public sealed record SourceStringsDocument
{
    public required string SourceLocale { get; init; }

    public required IReadOnlyList<SourceStringRow> Rows { get; init; }
}
