using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Localizer.Core.Lifecycle;

namespace Localizer.Desktop.ViewModels;

public partial class MessageViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;
}
