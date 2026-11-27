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
        if (_session.Catalog is null || !await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var selectedKey = SelectedEntry?.Key;
        var progress = new Progress<TranslationDraftProgress>(ReportDraftProgress);

        _busyStatus.Begin(busyStatus);
        await RunIoAsync(async token =>
        {
            var result = await _requestTranslationDrafts
                .ExecuteAsync(_session.Catalog, new RequestTranslationDraftsRequest { Filter = filter }, progress, token)
                .ConfigureAwait(true);

            if (!result.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Draft failed", UseCaseErrorText.Format(result)).ConfigureAwait(true);
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
            if (outcome.ReusedCount > 0)
            {
                StatusText += $" {outcome.ReusedCount} copied from approved matches.";
            }

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
        _busyStatus.Begin($"Drafting {value.Completed} of {value.Total}");
    }

    private async Task ApproveTranslationRowAsync(TranslationEditViewModel row)
    {
        await TryApproveTranslationRowAsync(row).ConfigureAwait(true);
        ReviewFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task TryApproveTranslationRowAsync(TranslationEditViewModel row)
    {
        if (_session.Catalog is null || string.IsNullOrWhiteSpace(EditorKey))
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

        // Checked after the commit, which turns edited text into a Draft and edited source into Stale.
        var keepsStaleText = row.IsStale;
        if (keepsStaleText && !ReferenceEquals(_staleConfirmation, row))
        {
            SelectReviewTarget(row.Locale);
            _staleConfirmation = row;
            StatusText =
                $"Source of '{key}' changed since the {row.Locale} text was written. Press F9 or Approve again to keep it.";
            return;
        }

        _staleConfirmation = null;
        var approveResult = keepsStaleText
            ? _approveStaleTranslation.Execute(_session.Catalog, new ApproveStaleTranslationRequest
            {
                Key = key,
                Locale = row.Locale
            })
            : _approveTranslation.Execute(_session.Catalog, new ApproveTranslationRequest
            {
                Key = key,
                Locale = row.Locale
            });

        if (!approveResult.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Approve failed", UseCaseErrorText.Format(approveResult)).ConfigureAwait(true);
            RunValidation();
            return;
        }

        IsDirty = true;
        RefreshEditorInPlace();
        SelectReviewTarget(row.Locale);
        StatusText = $"Approved '{key}' for {row.Locale}.";
        await OfferMatchingApprovalsAsync(_session.Catalog, key, row.Locale).ConfigureAwait(true);
    }

    private async Task InvalidateTranslationRowAsync(TranslationEditViewModel row)
    {
        await TryInvalidateTranslationRowAsync(row).ConfigureAwait(true);
        ReviewFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task TryInvalidateTranslationRowAsync(TranslationEditViewModel row)
    {
        if (_session.Catalog is null
            || string.IsNullOrWhiteSpace(EditorKey)
            || !await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var key = EditorKey;
        var result = _invalidateTranslation.Execute(_session.Catalog, new InvalidateTranslationRequest
        {
            Key = key,
            Locale = row.Locale
        });

        if (!result.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Invalidate failed", UseCaseErrorText.Format(result)).ConfigureAwait(true);
            return;
        }

        IsDirty = true;
        RefreshEditorInPlace();
        SelectReviewTarget(row.Locale);
        StatusText = $"Marked '{key}' ({row.Locale}) as Stale for re-draft.";
    }
}
