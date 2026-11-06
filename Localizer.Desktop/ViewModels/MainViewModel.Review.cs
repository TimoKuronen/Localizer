using CommunityToolkit.Mvvm.Input;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    private string? _reviewLocale;

    /// <summary>
    /// Raised after a review action so the view can return keyboard focus to the entry list.
    /// </summary>
    public event EventHandler? ReviewFocusRequested;

    public void SelectReviewTarget(string? locale)
    {
        _reviewLocale = locale;
        foreach (var translation in Translations)
        {
            translation.IsReviewTarget = locale is not null && translation.Locale == locale;
        }
    }

    [RelayCommand]
    private Task NextDraftAsync() => MoveToDraftAsync(_findAdjacentDraft.FindNext);

    [RelayCommand]
    private Task PreviousDraftAsync() => MoveToDraftAsync(_findAdjacentDraft.FindPrevious);

    // Guards CanRunCatalogCommands because keyboard shortcuts bypass the menu item's enabled state.
    [RelayCommand]
    private async Task ApproveReviewTargetAsync()
    {
        if (!CanRunCatalogCommands)
        {
            return;
        }

        var target = Translations.FirstOrDefault(translation => translation.IsReviewTarget);
        if (target is null)
        {
            StatusText = "Nothing highlighted to approve. Press F8 for the next draft.";
            return;
        }

        if (!target.CanApprove)
        {
            StatusText = target.ApproveHint;
            return;
        }

        await ApproveTranslationRowAsync(target).ConfigureAwait(true);
    }

    private async Task MoveToDraftAsync(Func<Catalog, string?, string?, WorkQueueItem?> findDraft)
    {
        if (_catalog is not { } catalog
            || !CanRunCatalogCommands
            || !await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var currentKey = string.IsNullOrWhiteSpace(EditorKey) ? null : EditorKey;
        var draft = findDraft(catalog, currentKey, _reviewLocale);
        if (draft is null)
        {
            StatusText = "No drafts left to review.";
            return;
        }

        var key = draft.Key.Value;
        if (key != EditorKey)
        {
            var row = Entries.FirstOrDefault(entry => entry.Key == key);
            SetSelectedEntryWithoutLoad(row);
            LoadSelectedEntry(row);
        }

        SelectReviewTarget(draft.Locale.Value);
        StatusText = $"Reviewing '{key}' ({draft.Locale.Value}).";
        ReviewFocusRequested?.Invoke(this, EventArgs.Empty);
    }
}
