using Localizer.Core.Syntax;

namespace Localizer.Application.UseCases;

public sealed record CreateCatalogRequest
{
    public required string CatalogId { get; init; }

    public required string SourceLocale { get; init; }

    public required IReadOnlyList<string> RequiredLocales { get; init; }

    public MessageSyntaxProfile DefaultSyntaxProfile { get; init; } = MessageSyntaxProfile.Plain;

    public int SchemaVersion { get; init; } = 1;
}
