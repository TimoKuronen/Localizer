namespace Localizer.Desktop.ViewModels;

public sealed record ShortcutInfo(string Keys, string Action);

public static class KeyboardShortcuts
{
    public static IReadOnlyList<ShortcutInfo> All { get; } =
    [
        new("Ctrl+N", "New catalog"),
        new("Ctrl+O", "Open catalog"),
        new("Ctrl+S", "Save"),
        new("Ctrl+Shift+S", "Save As"),
        new("F6", "Jump to the next Draft or Stale translation"),
        new("Shift+F6", "Jump to the previous Draft or Stale translation"),
        new("F7", "Edit the highlighted translation; press again for the next locale"),
        new("F9", "Approve the highlighted translation; press twice to keep unchanged Stale text")
    ];
}
