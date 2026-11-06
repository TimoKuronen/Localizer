using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Localizer.Desktop.ViewModels;

namespace Localizer.Desktop.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private IInputElement? _focusBeforeBusy;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.ReviewFocusRequested -= OnReviewFocusRequested;
            _viewModel.PropertyChanging -= OnViewModelPropertyChanging;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.ReviewFocusRequested += OnReviewFocusRequested;
            _viewModel.PropertyChanging += OnViewModelPropertyChanging;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    // The editor is disabled while busy, which clears focus, so it is captured before that happens.
    private void OnViewModelPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsBusy) && _viewModel?.IsBusy == false)
        {
            _focusBeforeBusy = FocusManager?.GetFocusedElement();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsBusy) && _viewModel?.IsBusy == false)
        {
            Dispatcher.UIThread.Post(RestoreFocusAfterBusy, DispatcherPriority.Loaded);
        }
    }

    private void RestoreFocusAfterBusy()
    {
        if (_focusBeforeBusy is InputElement { IsEffectivelyEnabled: true } element && this.IsVisualAncestorOf(element))
        {
            element.Focus();
        }
        else if (_focusBeforeBusy is not null)
        {
            EntriesGrid.Focus();
        }

        _focusBeforeBusy = null;
    }

    // Keeps F9 on the locale being edited rather than the one highlighted earlier.
    private void OnTranslationTextGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TranslationEditViewModel translation })
        {
            _viewModel?.SelectReviewTarget(translation.Locale);
        }
    }

    // Posted so rows rebuilt by the action exist before they are scrolled to.
    private void OnReviewFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ShowReviewPosition, DispatcherPriority.Loaded);

    private void ShowReviewPosition()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_viewModel.SelectedEntry is { } entry)
        {
            EntriesGrid.ScrollIntoView(entry, null);
        }

        var target = _viewModel.Translations.FirstOrDefault(translation => translation.IsReviewTarget);
        if (target is not null)
        {
            TranslationsList.ContainerFromItem(target)?.BringIntoView();
        }

        EntriesGrid.Focus();
    }
}
