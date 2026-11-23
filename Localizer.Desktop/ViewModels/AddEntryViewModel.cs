using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Localizer.Core.Lifecycle;

namespace Localizer.Desktop.ViewModels;

public partial class AddEntryViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeveloperNotes { get; set; } = string.Empty;
}
