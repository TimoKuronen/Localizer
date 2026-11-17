using Avalonia.Headless.NUnit;
using Localizer.Application.UseCases;
using Localizer.Desktop.Services;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class MainViewModelUnsavedChangesTests
{
    [AvaloniaTest]
    public void Open_OnACleanCatalog_DoesNotPrompt()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        var picksBefore = fixture.Dialogs.OpenPickCount;

        fixture.ViewModel.OpenCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.OpenPickCount == picksBefore + 1 && !fixture.ViewModel.IsBusy);

        Assert.That(fixture.Dialogs.UnsavedPromptCount, Is.Zero);
    }

    [AvaloniaTest]
    public void Open_WithUnsavedChanges_Cancel_KeepsTheCatalogAndSkipsThePicker()
    {
        var fixture = DirtyOpenCatalog();
        var picksBefore = fixture.Dialogs.OpenPickCount;
        fixture.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        fixture.ViewModel.OpenCatalogCommand.Execute(null);
        fixture.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.UnsavedPromptCount, Is.EqualTo(1));
            Assert.That(fixture.Dialogs.OpenPickCount, Is.EqualTo(picksBefore));
            Assert.That(fixture.ViewModel.HasUnsavedChanges, Is.True);
            Assert.That(fixture.Store.SavedPaths, Is.Empty);
        });
    }

    [AvaloniaTest]
    public void Open_WithUnsavedChanges_Discard_ContinuesWithoutSaving()
    {
        var fixture = DirtyOpenCatalog();
        var picksBefore = fixture.Dialogs.OpenPickCount;
        fixture.Dialogs.UnsavedChoice = UnsavedChangesChoice.Discard;

        fixture.ViewModel.OpenCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.OpenPickCount == picksBefore + 1 && !fixture.ViewModel.IsBusy);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Store.SavedPaths, Is.Empty);
            Assert.That(fixture.ViewModel.HasUnsavedChanges, Is.False);
        });
    }

    [AvaloniaTest]
    public void Open_WithUnsavedChanges_Save_WritesToTheKnownPathThenContinues()
    {
        var fixture = DirtyOpenCatalog();
        var picksBefore = fixture.Dialogs.OpenPickCount;
        fixture.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        fixture.ViewModel.OpenCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.OpenPickCount == picksBefore + 1 && !fixture.ViewModel.IsBusy);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Store.SavedPaths, Is.EqualTo(new[] { MainViewModelFixture.CatalogPath }));
            Assert.That(fixture.Dialogs.SavePickCount, Is.Zero);
        });
    }

    [AvaloniaTest]
    public void NewCatalog_WithUnsavedChanges_Cancel_DoesNotAskForANewCatalog()
    {
        var fixture = DirtyOpenCatalog();
        fixture.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        fixture.ViewModel.NewCatalogCommand.Execute(null);
        fixture.Pump();

        Assert.That(fixture.Dialogs.CreatePromptCount, Is.Zero);
    }

    [AvaloniaTest]
    public void NewCatalog_WithUnsavedChanges_Discard_AsksForANewCatalog()
    {
        var fixture = DirtyOpenCatalog();
        fixture.Dialogs.UnsavedChoice = UnsavedChangesChoice.Discard;

        fixture.ViewModel.NewCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.CreatePromptCount == 1);

        Assert.That(fixture.Store.SavedPaths, Is.Empty);
    }

    [AvaloniaTest]
    public void Save_OnACatalogWithoutAPath_AsksWhereToSaveAndBlocksWhenCancelled()
    {
        var fixture = NewUnsavedCatalog();
        fixture.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;
        fixture.Dialogs.SavePath = null;
        var createsBefore = fixture.Dialogs.CreatePromptCount;

        fixture.ViewModel.NewCatalogCommand.Execute(null);
        fixture.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.SavePickCount, Is.EqualTo(1));
            Assert.That(fixture.Dialogs.CreatePromptCount, Is.EqualTo(createsBefore));
            Assert.That(fixture.ViewModel.IsDirty, Is.True);
        });
    }

    [AvaloniaTest]
    public void Save_OnACatalogWithoutAPath_WritesToThePickedPathThenContinues()
    {
        var fixture = NewUnsavedCatalog();
        fixture.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;
        fixture.Dialogs.SavePath = "memory/picked.json";
        var createsBefore = fixture.Dialogs.CreatePromptCount;

        fixture.ViewModel.NewCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.CreatePromptCount == createsBefore + 1);

        Assert.That(fixture.Store.SavedPaths, Is.EqualTo(new[] { "memory/picked.json" }));
    }

    [AvaloniaTest]
    public void Prompt_NamesTheCatalogFileAndTheAction()
    {
        var fixture = DirtyOpenCatalog();
        fixture.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        fixture.ViewModel.OpenCatalogCommand.Execute(null);
        fixture.Pump();

        Assert.That(
            fixture.Dialogs.UnsavedPrompts.Single(),
            Does.Contain(MainViewModelFixture.CatalogPath).And.Contain("opening another catalog"));
    }

    [AvaloniaTest]
    public void ConfirmClose_OnACleanCatalog_AllowsCloseWithoutPrompt()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));

        var allowed = Confirm(fixture);

        Assert.Multiple(() =>
        {
            Assert.That(allowed, Is.True);
            Assert.That(fixture.Dialogs.UnsavedPromptCount, Is.Zero);
        });
    }

    [AvaloniaTest]
    public void ConfirmClose_WithNoCatalog_AllowsClose()
    {
        var fixture = new MainViewModelFixture();

        Assert.That(Confirm(fixture), Is.True);
        Assert.That(fixture.Dialogs.UnsavedPromptCount, Is.Zero);
    }

    [AvaloniaTest]
    public void ConfirmClose_WithUnsavedChanges_FollowsTheChoice()
    {
        var cancelled = DirtyOpenCatalog();
        cancelled.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        var discarded = DirtyOpenCatalog();
        discarded.Dialogs.UnsavedChoice = UnsavedChangesChoice.Discard;

        var saved = DirtyOpenCatalog();
        saved.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        Assert.Multiple(() =>
        {
            Assert.That(Confirm(cancelled), Is.False);
            Assert.That(Confirm(discarded), Is.True);
            Assert.That(Confirm(saved), Is.True);
            Assert.That(saved.Store.SavedPaths, Is.EqualTo(new[] { MainViewModelFixture.CatalogPath }));
        });
    }

    [AvaloniaTest]
    public void PendingEditorText_CountsAsUnsavedBeforeItIsCommitted()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Translation("es").Text = "Hola";

        Assert.That(fixture.ViewModel.HasUnsavedChanges, Is.True);
    }

    [AvaloniaTest]
    public void ConfirmClose_DuringAnOperation_BlocksWithAMessage()
    {
        var fixture = DirtyOpenCatalog();
        fixture.Drafts.Gate = new TaskCompletionSource();
        fixture.ViewModel.DraftAllCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.IsBusy);

        var allowed = Confirm(fixture);
        fixture.Drafts.Gate.SetResult();
        fixture.PumpUntil(() => !fixture.ViewModel.IsBusy);

        Assert.Multiple(() =>
        {
            Assert.That(allowed, Is.False);
            Assert.That(fixture.Dialogs.UnsavedPromptCount, Is.Zero);
            Assert.That(fixture.Dialogs.Messages, Has.Some.Contains("Operation in progress"));
        });
    }

    private static MainViewModelFixture DirtyOpenCatalog()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Translation("es").Text = "Hola";
        return fixture;
    }

    private static MainViewModelFixture NewUnsavedCatalog()
    {
        var fixture = new MainViewModelFixture();
        fixture.Dialogs.CreateResult = new CreateCatalogRequest
        {
            CatalogId = "fresh",
            SourceLocale = "en",
            RequiredLocales = ["es"]
        };
        fixture.ViewModel.NewCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.HasCatalog);
        return fixture;
    }

    private static bool Confirm(MainViewModelFixture fixture)
    {
        var task = fixture.ViewModel.ConfirmCloseAsync();
        fixture.PumpUntil(() => task.IsCompleted);
        return task.Result;
    }
}
