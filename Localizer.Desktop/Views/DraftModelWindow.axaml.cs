using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Localizer.Desktop.Views;

public partial class DraftModelWindow : Window
{
    public DraftModelWindow()
    {
        InitializeComponent();
    }

    private void OnUseClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
