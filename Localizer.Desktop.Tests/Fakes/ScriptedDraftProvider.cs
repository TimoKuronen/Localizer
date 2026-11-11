using Localizer.Application.Drafting;

namespace Localizer.Desktop.Tests.Fakes;

/// <summary>
/// Drafts every requested item as "Draft {key} {locale}". Reports progress for each item and, when a gate is set,
/// holds the batch open after reporting so tests can observe the busy state.
/// </summary>
internal sealed class ScriptedDraftProvider : ITranslationDraftProvider
{
    public TaskCompletionSource? Gate { get; set; }

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IProgress<TranslationDraftProgress>? LastProgress { get; private set; }

    public Task<TranslationDraftBatchResult> DraftAsync(
        TranslationDraftBatchRequest request,
        IProgress<TranslationDraftProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        DraftCoreAsync(request, progress, cancellationToken);

    private async Task<TranslationDraftBatchResult> DraftCoreAsync(
        TranslationDraftBatchRequest request,
        IProgress<TranslationDraftProgress>? progress,
        CancellationToken cancellationToken)
    {
        LastProgress = progress;
        Started.TrySetResult();

        var total = request.Items.Count;
        for (var completed = 1; completed <= total; completed++)
        {
            progress?.Report(new TranslationDraftProgress(completed, total));
        }

        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new TranslationDraftBatchResult
        {
            ProviderName = "test",
            ModelName = "test-model",
            Items = request.Items
                .Select(item => new TranslationDraftItemResult
                {
                    Key = item.Key,
                    TargetLocale = item.TargetLocale,
                    Text = $"Draft {item.Key} {item.TargetLocale}"
                })
                .ToList()
        };
    }
}
