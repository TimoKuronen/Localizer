using Avalonia.Headless.NUnit;
using Localizer.Application.UseCases;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class MainViewModelSaveTests
{
    [AvaloniaTest]
    public void OpenedCatalog_StartsCleanWithSaveDisabled()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.IsDirty, Is.False);
            Assert.That(fixture.ViewModel.CanSave, Is.False);
            Assert.That(fixture.ViewModel.CanSaveAs, Is.True);
            Assert.That(fixture.ViewModel.WindowTitle, Is.EqualTo($"Localizer - {MainViewModelFixture.CatalogPath}"));
        });
    }

    [AvaloniaTest]
    public void EditingTranslationText_EnablesSaveAndMarksTheTitle()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));

        fixture.Translation("es").Text = "Hola";

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.CanSave, Is.True);
            Assert.That(fixture.ViewModel.WindowTitle, Does.EndWith("*"));
        });
    }

    [AvaloniaTest]
    public void Save_WritesToTheKnownPathWithoutAskingForOne()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Translation("es").Text = "Hola";

        fixture.ViewModel.SaveCatalogCommand.Execute(null);
        fixture.PumpUntil(() => !fixture.ViewModel.IsDirty && !fixture.ViewModel.IsBusy);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Store.SavedPaths, Is.EqualTo(new[] { MainViewModelFixture.CatalogPath }));
            Assert.That(fixture.Dialogs.SavePickCount, Is.Zero);
            Assert.That(fixture.ViewModel.CanSave, Is.False);
            Assert.That(fixture.ViewModel.StatusText, Is.EqualTo($"Saved {MainViewModelFixture.CatalogPath}"));
        });
    }

    [AvaloniaTest]
    public void Save_WithNothingToSave_DoesNotWrite()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));

        fixture.ViewModel.SaveCatalogCommand.Execute(null);
        fixture.Pump();

        Assert.That(fixture.Store.SavedPaths, Is.Empty);
    }

    [AvaloniaTest]
    public void NewCatalog_IsDirtyButNeedsSaveAs()
    {
        var fixture = new MainViewModelFixture();
        fixture.Dialogs.CreateResult = NewCatalogRequest();

        fixture.ViewModel.NewCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.HasCatalog);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.IsDirty, Is.True);
            Assert.That(fixture.ViewModel.CanSave, Is.False);
            Assert.That(fixture.ViewModel.CanSaveAs, Is.True);
            Assert.That(fixture.ViewModel.WindowTitle, Does.Contain("(unsaved)"));
        });
    }

    [AvaloniaTest]
    public void SaveAs_OnNewCatalog_WritesAndClearsDirty()
    {
        var fixture = new MainViewModelFixture();
        fixture.Dialogs.CreateResult = NewCatalogRequest();
        fixture.ViewModel.NewCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.HasCatalog);
        fixture.Dialogs.SavePath = "memory/new.json";

        fixture.ViewModel.SaveCatalogAsCommand.Execute(null);
        fixture.PumpUntil(() => !fixture.ViewModel.IsDirty && !fixture.ViewModel.IsBusy);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Store.SavedPaths, Is.EqualTo(new[] { "memory/new.json" }));
            Assert.That(fixture.ViewModel.WindowTitle, Is.EqualTo("Localizer - memory/new.json"));
        });
    }

    [AvaloniaTest]
    public void SaveAs_CancelledPicker_KeepsTheCatalogDirty()
    {
        var fixture = new MainViewModelFixture();
        fixture.Dialogs.CreateResult = NewCatalogRequest();
        fixture.ViewModel.NewCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.HasCatalog);
        fixture.Dialogs.SavePath = null;

        fixture.ViewModel.SaveCatalogAsCommand.Execute(null);
        fixture.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.SavePickCount, Is.EqualTo(1));
            Assert.That(fixture.Store.SavedPaths, Is.Empty);
            Assert.That(fixture.ViewModel.IsDirty, Is.True);
        });
    }

    private static CreateCatalogRequest NewCatalogRequest() =>
        new()
        {
            CatalogId = "fresh",
            SourceLocale = "en",
            RequiredLocales = ["es"]
        };
}
