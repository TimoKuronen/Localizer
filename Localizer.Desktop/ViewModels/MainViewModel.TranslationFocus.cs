using CommunityToolkit.Mvvm.Input;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// Raised when the view should move keyboard focus into a translation text box.
    /// </summary>
    public event EventHandler? TranslationEditRequested;

    /// <summary>
    /// The row to edit next: the one after the row already being edited, otherwise the highlighted
    /// row, the first one awaiting review, or the first one.
    /// </summary>
    public TranslationEditViewModel? FindEditTarget(TranslationEditViewModel? editing)
    {
        if (editing is not null && Translations.IndexOf(editing) is var index and >= 0)
        {
            return Translations[(index + 1) % Translations.Count];
        }

        return Translations.FirstOrDefault(translation => translation.IsReviewTarget)
            ?? Translations.FirstOrDefault(translation => translation.IsDraft || translation.IsStale)
            ?? Translations.FirstOrDefault();
    }

    // Guards CanRunCatalogCommands because keyboard shortcuts bypass the menu item's enabled state.
    [RelayCommand]
    private void EditTranslation()
    {
        if (CanRunCatalogCommands)
        {
            TranslationEditRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
