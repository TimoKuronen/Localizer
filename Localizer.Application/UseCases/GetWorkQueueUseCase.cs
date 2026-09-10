using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;

namespace Localizer.Application.UseCases;

[Flags]
public enum WorkQueueFilter
{
    None = 0,
    Missing = 1,
    Stale = 2,
    Draft = 4,
    Unfinished = Missing | Stale | Draft
}

public sealed record WorkQueueItem(
    EntryKey Key,
    Locale Locale,
    TranslationEffectiveStatus Status,
    string SourceText);

public sealed class GetWorkQueueUseCase
{
    public IReadOnlyList<WorkQueueItem> Execute(
        Catalog catalog,
        WorkQueueFilter filter = WorkQueueFilter.Unfinished)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (filter == WorkQueueFilter.None)
        {
            return [];
        }

        var items = new List<WorkQueueItem>();

        foreach (var entry in catalog.Entries.Values.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal))
        {
            foreach (var locale in catalog.RequiredLocales.OrderBy(locale => locale.Value, StringComparer.Ordinal))
            {
                var status = catalog.GetEffectiveStatus(entry, locale);
                if (!Matches(filter, status))
                {
                    continue;
                }

                items.Add(new WorkQueueItem(entry.Key, locale, status, entry.SourceText));
            }
        }

        return items;
    }

    private static bool Matches(WorkQueueFilter filter, TranslationEffectiveStatus status) =>
        status switch
        {
            TranslationEffectiveStatus.Missing => filter.HasFlag(WorkQueueFilter.Missing),
            TranslationEffectiveStatus.Stale => filter.HasFlag(WorkQueueFilter.Stale),
            TranslationEffectiveStatus.Draft => filter.HasFlag(WorkQueueFilter.Draft),
            _ => false
        };
}
