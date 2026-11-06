using Avalonia;
using Avalonia.Headless;
using Localizer.Desktop;
using Localizer.Desktop.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Localizer.Desktop.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
