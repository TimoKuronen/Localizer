using CommunityToolkit.Mvvm.Input;
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
        if (_catalog is null)
        {
            return;
        }

        var selectedKey = SelectedEntry?.Key;

        BeginBusyStatus(busyStatus);
        await RunIoAsync(async token =>
        {
            var result = await _requestTranslationDrafts
                .ExecuteAsync(_catalog, new RequestTranslationDraftsRequest { Filter = filter }, token)
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

    private async Task ApproveTranslationRowAsync(TranslationEditViewModel row)
    {
        if (_catalog is null || string.IsNullOrWhiteSpace(EditorKey))
        {
            return;
        }

        var key = EditorKey;

        if (row.HasTextChanged)
        {
            if (string.IsNullOrEmpty(row.Text))
            {
                await _dialogs.ShowMessageAsync(
                    "Approve failed",
                    "Translation text is empty. Enter text before approving.").ConfigureAwait(true);
                return;
            }

            var draftResult = _setTranslationDraft.Execute(_catalog, new SetTranslationDraftRequest
            {
                Key = key,
                Locale = row.Locale,
                Text = row.Text
            });

            if (!draftResult.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Approve failed", FormatError(draftResult)).ConfigureAwait(true);
                return;
            }
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
        RefreshFromCatalog(selectKey: key);
        RunValidation();
        StatusText = $"Approved '{key}' for {row.Locale}.";
    }

    private async Task InvalidateTranslationRowAsync(TranslationEditViewModel row)
    {
        if (_catalog is null || string.IsNullOrWhiteSpace(EditorKey))
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
        RefreshFromCatalog(selectKey: key);
        RunValidation();
        StatusText = $"Marked '{key}' ({row.Locale}) as Stale for re-draft.";
    }
}
