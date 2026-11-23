namespace Localizer.Application.Drafting;

public interface ITranslationDraftProvider
{
    Task<TranslationDraftBatchResult> DraftAsync(
        TranslationDraftBatchRequest request,
        IProgress<TranslationDraftProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
