using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Localizer.Desktop.ViewModels;

public partial class DraftModelViewModel : ViewModelBase
{
    public const string AutomaticOption = "Automatic (sole installed model)";

    public DraftModelViewModel(IReadOnlyList<string> models, string? savedModel)
    {
        Options = [AutomaticOption, .. models];
        SelectedOption = savedModel is not null && models.Contains(savedModel, StringComparer.Ordinal)
            ? savedModel
            : AutomaticOption;
    }

    public ObservableCollection<string> Options { get; }

    [ObservableProperty]
    public partial string SelectedOption { get; set; }

    public string? SelectedModelName =>
        string.Equals(SelectedOption, AutomaticOption, StringComparison.Ordinal) ? null : SelectedOption;
}
