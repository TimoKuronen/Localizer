using CommunityToolkit.Mvvm.Input;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private Task ShowKeyboardShortcutsAsync() =>
        _dialogs.ShowShortcutsAsync(KeyboardShortcuts.All);

    [RelayCommand]
    private Task ShowWorkflowGuideAsync() =>
        _dialogs.ShowWorkflowGuideAsync(WorkflowGuide.All);
}
