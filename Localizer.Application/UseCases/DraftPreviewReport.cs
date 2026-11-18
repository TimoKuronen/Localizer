namespace Localizer.Application.UseCases;

public sealed record DraftPreviewSkippedItem
{
    public enum SkipReason
    {
        Missing,
        InvalidTranslation
    }

    public required string Key { get; init; }

    public required string Locale { get; init; }

    public required SkipReason Reason { get; init; }

    public required string Message { get; init; }
}

public sealed record DraftPreviewLocaleSummary
{
    public required string Locale { get; init; }

    public required int ApprovedCount { get; init; }

    public required int DraftCount { get; init; }

    public required int StaleCount { get; init; }

    public required int MissingCount { get; init; }

    public required int InvalidCount { get; init; }

    public int IncludedCount => ApprovedCount + DraftCount + StaleCount;
}

public sealed record DraftPreviewReport
{
    public static DraftPreviewReport Empty { get; } = new()
    {
        LocaleSummaries = [],
        SkippedItems = []
    };

    public required IReadOnlyList<DraftPreviewLocaleSummary> LocaleSummaries { get; init; }

    public required IReadOnlyList<DraftPreviewSkippedItem> SkippedItems { get; init; }
}
