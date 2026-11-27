using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Localizer.Core.Lifecycle;

namespace Localizer.Desktop.ViewModels;

public partial class EntryRowViewModel : ViewModelBase
{
    public EntryRowViewModel(string key, string sourceText, string localeStatuses)
    {
        Key = key;
        SourceText = sourceText;
        LocaleStatuses = localeStatuses;
    }

    public string Key { get; }

    [ObservableProperty]
    public partial string SourceText { get; set; }

    [ObservableProperty]
    public partial string LocaleStatuses { get; set; }

    /// <summary>
    /// "x3" when three entries share this source; empty when the source is unique.
    /// </summary>
    [ObservableProperty]
    public partial string SameSource { get; set; } = string.Empty;
}
