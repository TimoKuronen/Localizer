namespace Localizer.Desktop.ViewModels;

public sealed class ShortcutsViewModel : ViewModelBase
{
    public ShortcutsViewModel(IReadOnlyList<ShortcutInfo> shortcuts)
    {
        Shortcuts = shortcuts ?? throw new ArgumentNullException(nameof(shortcuts));
    }

    public IReadOnlyList<ShortcutInfo> Shortcuts { get; }
}
