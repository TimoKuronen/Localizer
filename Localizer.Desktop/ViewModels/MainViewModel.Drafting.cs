using CommunityToolkit.Mvvm.Input;
using Localizer.Application.Drafting;
using Localizer.Application.UseCases;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task DraftMissingAndStaleAsync(CancellationToken cancellationToken) =>
        await DraftAsync(
            WorkQueueFilter.Missing | WorkQueueFilter.Stale,
            "Requesting local model drafts for Missing and Stale items",
            cancellationToken).ConfigureAwait(true);

    [RelayCommand]
    private async Task DraftAllAsync(CancellationToken cancellationToken) =>
        await DraftAsync(
            WorkQueueFilter.AllStatuses,
            "Requesting local model drafts for all locales",
            cancellationToken).ConfigureAwait(true);

    private async Task DraftAsync(
        WorkQueueFilter filter,
        string busyStatus,
        CancellationToken cancellationToken)
    {
        if (_catalog is null || !await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var selectedKey = SelectedEntry?.Key;
        var progress = new Progress<TranslationDraftProgress>(ReportDraftProgress);

        BeginBusyStatus(busyStatus);
        await RunIoAsync(async token =>
        {
            var result = await _requestTranslationDrafts
                .ExecuteAsync(_catalog, new RequestTranslationDraftsRequest { Filter = filter }, progress, token)
                .ConfigureAwait(true);

            if (!result.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Draft failed", FormatError(result)).ConfigureAwait(true);
                StatusText = "Drafting failed.";
                return;
            }

            var outcome = result.Value!;
            IsDirty = outcome.AppliedCount > 0 || IsDirty;
            RefreshFromCatalog(selectKey: selectedKey);
            RunValidation();

            StatusText = outcome.RequestedCount == 0
                ? "No matching items to draft."
                : $"Drafted {outcome.AppliedCount} of {outcome.RequestedCount} item(s).";

            if (outcome.RejectionMessages.Count > 0)
            {
                var detail = string.Join(Environment.NewLine, outcome.RejectionMessages.Take(12));
                if (outcome.RejectionMessages.Count > 12)
                {
                    detail += $"{Environment.NewLine}...and {outcome.RejectionMessages.Count - 12} more.";
                }

                await _dialogs.ShowMessageAsync(
                    "Drafting finished with rejections",
                    detail).ConfigureAwait(true);
            }
        }, cancellationToken).ConfigureAwait(true);
    }

    // Progress<T> posts to the UI thread, so a late report can arrive after the operation ended.
    private void ReportDraftProgress(TranslationDraftProgress value)
    {
        if (!IsBusy || value.Total <= 0)
        {
            return;
        }

        IsBusyProgressIndeterminate = false;
        BusyProgressMaximum = value.Total;
        BusyProgressValue = value.Completed;
        BeginBusyStatus($"Drafting {value.Completed} of {value.Total}");
    }

    private async Task ApproveTranslationRowAsync(TranslationEditViewModel row)
    {
        await TryApproveTranslationRowAsync(row).ConfigureAwait(true);
        ReviewFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task TryApproveTranslationRowAsync(TranslationEditViewModel row)
    {
        if (_catalog is null || string.IsNullOrWhiteSpace(EditorKey))
        {
            return;
        }

        var key = EditorKey;

        if (string.IsNullOrEmpty(row.Text))
        {
            await _dialogs.ShowMessageAsync(
                "Approve failed",
                "Translation text is empty. Enter text before approving.").ConfigureAwait(true);
            return;
        }

        if (!await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var approveResult = _approveTranslation.Execute(_catalog, new ApproveTranslationRequest
        {
            Key = key,
            Locale = row.Locale
        });

        if (!approveResult.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Approve failed", FormatError(approveResult)).ConfigureAwait(true);
            RunValidation();
            return;
        }

        IsDirty = true;
        RefreshEditorInPlace();
        SelectReviewTarget(row.Locale);
        StatusText = $"Approved '{key}' for {row.Locale}.";
    }

    private async Task InvalidateTranslationRowAsync(TranslationEditViewModel row)
    {
        await TryInvalidateTranslationRowAsync(row).ConfigureAwait(true);
        ReviewFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task TryInvalidateTranslationRowAsync(TranslationEditViewModel row)
    {
        if (_catalog is null
            || string.IsNullOrWhiteSpace(EditorKey)
            || !await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var key = EditorKey;
        var result = _invalidateTranslation.Execute(_catalog, new InvalidateTranslationRequest
        {
            Key = key,
            Locale = row.Locale
        });

        if (!result.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Invalidate failed", FormatError(result)).ConfigureAwait(true);
            return;
        }

        IsDirty = true;
        RefreshEditorInPlace();
        SelectReviewTarget(row.Locale);
        StatusText = $"Marked '{key}' ({row.Locale}) as Stale for re-draft.";
    }
}
