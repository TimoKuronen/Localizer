using CommunityToolkit.Mvvm.ComponentModel;

namespace Localizer.Desktop.ViewModels;

public partial class ChecklistItemViewModel : ViewModelBase
{
    public ChecklistItemViewModel(string id, string detail, bool isSelected)
    {
        Id = id;
        Detail = detail;
        IsSelected = isSelected;
    }

    public string Id { get; }

    public string Detail { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
