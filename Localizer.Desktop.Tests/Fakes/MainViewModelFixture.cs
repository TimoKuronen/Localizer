using Avalonia.Threading;
using Localizer.Application.Project;
using Localizer.Core.Catalogs;
using Localizer.Desktop.ViewModels;

namespace Localizer.Desktop.Tests.Fakes;

/// <summary>
/// A MainViewModel wired to in-memory fakes and no window. Tests must run under the Avalonia test dispatcher
/// because the view model uses dispatcher timers and posted progress callbacks.
/// </summary>
internal sealed class MainViewModelFixture
{
    public const string CatalogPath = "memory/catalog.json";

    private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(10);

    public MainViewModelFixture()
    {
        ViewModel = MainViewModelFactory.Create(Dialogs, Store, ProjectSettings, Drafts);
    }

    public RecordingDialogs Dialogs { get; } = new();

    public InMemoryCatalogStore Store { get; } = new();

    public InMemoryProjectFolderSettings ProjectSettings { get; } = new();

    public ScriptedDraftProvider Drafts { get; } = new();

    public MainViewModel ViewModel { get; }

    public static MainViewModelFixture Open(Catalog catalog, ProjectFolderBinding? binding = null)
    {
        var fixture = new MainViewModelFixture();
        fixture.Store.Put(CatalogPath, catalog);
        if (binding is not null)
        {
            fixture.ProjectSettings.Put(binding);
        }

        fixture.Dialogs.OpenPath = CatalogPath;
        fixture.ViewModel.OpenCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.HasCatalog && !fixture.ViewModel.IsBusy);
        return fixture;
    }

    public TranslationEditViewModel Translation(string locale) =>
        ViewModel.Translations.Single(translation => translation.Locale == locale);

    public void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + PumpTimeout;
        while (!condition())
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "Timed out waiting for the view model.");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }

        Dispatcher.UIThread.RunJobs();
    }

    public void Pump() => Dispatcher.UIThread.RunJobs();
}
