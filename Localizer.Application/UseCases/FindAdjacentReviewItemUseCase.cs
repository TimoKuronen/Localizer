using Localizer.Core.Catalogs;

namespace Localizer.Application.UseCases;

/// <summary>
/// Steps through Draft and Stale items in work-queue order, wrapping at either end. The current
/// position does not have to be one of them, so review can continue from an item just approved.
/// </summary>
public sealed class FindAdjacentReviewItemUseCase
{
    private readonly GetWorkQueueUseCase _getWorkQueue;

    public FindAdjacentReviewItemUseCase(GetWorkQueueUseCase getWorkQueue)
    {
        _getWorkQueue = getWorkQueue ?? throw new ArgumentNullException(nameof(getWorkQueue));
    }

    public WorkQueueItem? FindNext(Catalog catalog, string? currentKey, string? currentLocale)
    {
        var items = GetReviewItems(catalog);
        return items.FirstOrDefault(item => CompareToPosition(item, currentKey, currentLocale) > 0)
            ?? items.FirstOrDefault();
    }

    public WorkQueueItem? FindPrevious(Catalog catalog, string? currentKey, string? currentLocale)
    {
        var items = GetReviewItems(catalog);
        return items.LastOrDefault(item => CompareToPosition(item, currentKey, currentLocale) < 0)
            ?? items.LastOrDefault();
    }

    private IReadOnlyList<WorkQueueItem> GetReviewItems(Catalog catalog) =>
        _getWorkQueue.Execute(catalog, WorkQueueFilter.Draft | WorkQueueFilter.Stale);

    private static int CompareToPosition(WorkQueueItem item, string? key, string? locale)
    {
        var byKey = string.CompareOrdinal(item.Key.Value, key);
        return byKey != 0 ? byKey : string.CompareOrdinal(item.Locale.Value, locale);
    }
}
