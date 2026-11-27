using CommunityToolkit.Mvvm.Input;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    private string? _reviewLocale;

    // Row whose unchanged Stale text has been asked about; approving it again keeps the text.
    private TranslationEditViewModel? _staleConfirmation;

    /// <summary>
    /// Raised after a review action so the view can return keyboard focus to the entry list.
    /// </summary>
    public event EventHandler? ReviewFocusRequested;

    public void SelectReviewTarget(string? locale)
    {
        _reviewLocale = locale;
        if (_staleConfirmation?.Locale != locale)
        {
            _staleConfirmation = null;
        }

        foreach (var translation in Translations)
        {
            translation.IsReviewTarget = locale is not null && translation.Locale == locale;
        }
    }

    [RelayCommand]
    private Task NextReviewItemAsync() => MoveToReviewItemAsync(_findAdjacentReviewItem.FindNext);

    [RelayCommand]
    private Task PreviousReviewItemAsync() => MoveToReviewItemAsync(_findAdjacentReviewItem.FindPrevious);

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
            StatusText = "Nothing highlighted to approve. Press F6 for the next item to review.";
            return;
        }

        if (!target.CanApprove)
        {
            StatusText = target.ApproveHint;
            return;
        }

        await ApproveTranslationRowAsync(target).ConfigureAwait(true);
    }

    private async Task MoveToReviewItemAsync(Func<Catalog, string?, string?, WorkQueueItem?> findItem)
    {
        if (_session.Catalog is not { } catalog
            || !CanRunCatalogCommands
            || !await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var currentKey = string.IsNullOrWhiteSpace(EditorKey) ? null : EditorKey;
        _staleConfirmation = null;
        var item = findItem(catalog, currentKey, _reviewLocale);
        if (item is null)
        {
            StatusText = "Nothing left to review.";
            return;
        }

        var key = item.Key.Value;
        if (key != EditorKey)
        {
            var row = Entries.FirstOrDefault(entry => entry.Key == key);
            SetSelectedEntryWithoutLoad(row);
            LoadSelectedEntry(row);
        }

        SelectReviewTarget(item.Locale.Value);
        StatusText = $"Reviewing '{key}' ({item.Locale.Value}, {item.Status}).";
        ReviewFocusRequested?.Invoke(this, EventArgs.Empty);
    }
}
