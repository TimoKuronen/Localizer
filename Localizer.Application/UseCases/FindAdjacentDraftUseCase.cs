using Localizer.Core.Catalogs;

namespace Localizer.Application.UseCases;

/// <summary>
/// Steps through Draft items in work-queue order, wrapping at either end. The current position
/// does not have to be a Draft, so review can continue from an item that was just approved.
/// </summary>
public sealed class FindAdjacentDraftUseCase
{
    private readonly GetWorkQueueUseCase _getWorkQueue;

    public FindAdjacentDraftUseCase(GetWorkQueueUseCase getWorkQueue)
    {
        _getWorkQueue = getWorkQueue ?? throw new ArgumentNullException(nameof(getWorkQueue));
    }

    public WorkQueueItem? FindNext(Catalog catalog, string? currentKey, string? currentLocale)
    {
        var drafts = _getWorkQueue.Execute(catalog, WorkQueueFilter.Draft);
        return drafts.FirstOrDefault(item => CompareToPosition(item, currentKey, currentLocale) > 0)
            ?? drafts.FirstOrDefault();
    }

    public WorkQueueItem? FindPrevious(Catalog catalog, string? currentKey, string? currentLocale)
    {
        var drafts = _getWorkQueue.Execute(catalog, WorkQueueFilter.Draft);
        return drafts.LastOrDefault(item => CompareToPosition(item, currentKey, currentLocale) < 0)
            ?? drafts.LastOrDefault();
    }

    private static int CompareToPosition(WorkQueueItem item, string? key, string? locale)
    {
        var byKey = string.CompareOrdinal(item.Key.Value, key);
        return byKey != 0 ? byKey : string.CompareOrdinal(item.Locale.Value, locale);
    }
}
