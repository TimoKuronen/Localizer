using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Localizer.Desktop.Views;

public partial class AddEntryWindow : Window
{
    public AddEntryWindow()
    {
        InitializeComponent();
    }

    private void OnAddClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
