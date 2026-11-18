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
        new("F8", "Jump to the next draft"),
        new("Shift+F8", "Jump to the previous draft"),
        new("F9", "Approve the highlighted translation")
    ];
}
