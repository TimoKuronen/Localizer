namespace Localizer.Application.Drafting;

public sealed record TranslationDraftBatchResult
{
    public required string ProviderName { get; init; }

    public required string ModelName { get; init; }

    public required IReadOnlyList<TranslationDraftItemResult> Items { get; init; }

    public IReadOnlyList<string> ItemFailures { get; init; } = [];
}

// Completed counts every attempted item, whether it produced a draft or a failure.
