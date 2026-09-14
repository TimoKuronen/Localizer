using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Localizer.Desktop.Composition;
using Localizer.Desktop.Views;

namespace Localizer.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var (mainViewModel, dialogs) = AppComposition.Create();
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            dialogs.Attach(mainWindow);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
