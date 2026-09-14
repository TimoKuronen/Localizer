using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Localizer.Desktop.Views;

public partial class NewCatalogWindow : Window
{
    public NewCatalogWindow()
    {
        InitializeComponent();
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
