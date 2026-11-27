using Avalonia.Controls;
using Avalonia.Interactivity;
using Localizer.Desktop.ViewModels;

namespace Localizer.Desktop.Views;

public partial class ChecklistWindow : Window
{
    public ChecklistWindow()
    {
        InitializeComponent();
    }

    // Focus starts on the default button so Enter acts without a click.
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is ChecklistViewModel viewModel)
        {
            (viewModel.ConfirmIsDefault ? ConfirmButton : CancelButton).Focus();
        }
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
