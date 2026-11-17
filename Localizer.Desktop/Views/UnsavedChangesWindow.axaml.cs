using Avalonia.Controls;
using Avalonia.Interactivity;
using Localizer.Desktop.Services;

namespace Localizer.Desktop.Views;

public partial class UnsavedChangesWindow : Window
{
    public UnsavedChangesWindow()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Save);

    private void OnDiscardClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Discard);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Cancel);
}
