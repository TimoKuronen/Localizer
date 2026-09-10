using Localizer.Core.Catalogs;
using Localizer.Core.Lifecycle;

namespace Localizer.Application.UseCases;

public sealed record CatalogStatusSummary
{
    public required int EntryCount { get; init; }

    public required int MissingCount { get; init; }

    public required int StaleCount { get; init; }

    public required int DraftCount { get; init; }

    public required int ApprovedCount { get; init; }

    public int RequiredLocaleSlotCount =>
        MissingCount + StaleCount + DraftCount + ApprovedCount;
}

public sealed class GetCatalogStatusSummaryUseCase
{
    public CatalogStatusSummary Execute(Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var missing = 0;
        var stale = 0;
        var draft = 0;
        var approved = 0;

        foreach (var entry in catalog.Entries.Values)
        {
            foreach (var locale in catalog.RequiredLocales)
            {
                switch (catalog.GetEffectiveStatus(entry, locale))
                {
                    case TranslationEffectiveStatus.Missing:
                        missing++;
                        break;
                    case TranslationEffectiveStatus.Stale:
                        stale++;
                        break;
                    case TranslationEffectiveStatus.Draft:
                        draft++;
                        break;
                    case TranslationEffectiveStatus.Approved:
                        approved++;
                        break;
                }
            }
        }

        return new CatalogStatusSummary
        {
            EntryCount = catalog.Entries.Count,
            MissingCount = missing,
            StaleCount = stale,
            DraftCount = draft,
            ApprovedCount = approved
        };
    }
}
