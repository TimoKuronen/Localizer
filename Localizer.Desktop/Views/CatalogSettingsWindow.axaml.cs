using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Localizer.Desktop.Views;

public partial class CatalogSettingsWindow : Window
{
    public CatalogSettingsWindow()
    {
        InitializeComponent();
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
