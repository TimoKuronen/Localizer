using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Localizer.Core.Lifecycle;

namespace Localizer.Desktop.ViewModels;

public partial class NewCatalogViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string CatalogId { get; set; } = "catalog";

    [ObservableProperty]
    public partial string SourceLocale { get; set; } = "en";

    [ObservableProperty]
    public partial string RequiredLocalesText { get; set; } = "es";

    [ObservableProperty]
    public partial bool UseCompositeSyntax { get; set; }
}
