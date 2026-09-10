using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;

namespace Localizer.Application.UseCases;

public sealed record AddCatalogEntryRequest
{
    public required string Key { get; init; }

    public required string SourceText { get; init; }

    public string? Category { get; init; }

    public bool AllowEmptyText { get; init; }

    public string? DeveloperNotes { get; init; }

    public TranslationContext? Context { get; init; }

    public EntryConstraints? Constraints { get; init; }

    public MessageSyntaxProfile? SyntaxProfileOverride { get; init; }

    public ExternalIds? ExternalIds { get; init; }
}

public sealed class AddCatalogEntryUseCase
{
    public UseCaseResult Execute(Catalog catalog, AddCatalogEntryRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        return DomainGuard.Try(() =>
        {
            var entry = new CatalogEntry
            {
                Key = EntryKey.Create(request.Key),
                SourceText = request.SourceText,
                Category = request.Category,
                AllowEmptyText = request.AllowEmptyText,
                DeveloperNotes = request.DeveloperNotes,
                Context = request.Context ?? new TranslationContext(),
                Constraints = request.Constraints ?? new EntryConstraints(),
                SyntaxProfileOverride = request.SyntaxProfileOverride,
                ExternalIds = request.ExternalIds ?? new ExternalIds(),
                Translations = new Dictionary<Locale, Translation>()
            };

            catalog.AddEntry(entry);
        });
    }
}
