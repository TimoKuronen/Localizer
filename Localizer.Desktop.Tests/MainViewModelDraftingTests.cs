using Avalonia.Headless.NUnit;
using Localizer.Application.Drafting;
using Localizer.Application.Project;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class MainViewModelDraftingTests
{
    [AvaloniaTest]
    public void WhileDrafting_EditingSavingAndCatalogCommandsAreBlocked()
    {
        var fixture = OpenWithPendingEdit();
        fixture.Drafts.Gate = new TaskCompletionSource();

        fixture.ViewModel.DraftMissingAndStaleCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.IsBusy && fixture.Drafts.Started.Task.IsCompleted);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.IsDirty, Is.True);
            Assert.That(fixture.ViewModel.CanSave, Is.False);
            Assert.That(fixture.ViewModel.CanSaveAs, Is.False);
            Assert.That(fixture.ViewModel.CanEditSelectedEntry, Is.False);
            Assert.That(fixture.ViewModel.CanRunCatalogCommands, Is.False);
            Assert.That(fixture.ViewModel.CanRunProjectCommands, Is.False);
        });

        fixture.Drafts.Gate.SetResult();
        fixture.PumpUntil(() => !fixture.ViewModel.IsBusy);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.CanSave, Is.True);
            Assert.That(fixture.ViewModel.CanEditSelectedEntry, Is.True);
            Assert.That(fixture.ViewModel.CanRunCatalogCommands, Is.True);
            Assert.That(fixture.ViewModel.CanRunProjectCommands, Is.True);
        });
    }

    [AvaloniaTest]
    public void WhileDrafting_ProgressShowsCompletedOfTotal()
    {
        var fixture = OpenWithPendingEdit();
        fixture.Drafts.Gate = new TaskCompletionSource();

        fixture.ViewModel.DraftMissingAndStaleCommand.Execute(null);
        fixture.PumpUntil(() => !fixture.ViewModel.IsBusyProgressIndeterminate);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.StatusText, Does.StartWith("Drafting 1 of 1"));
            Assert.That(fixture.ViewModel.BusyProgressMaximum, Is.EqualTo(1));
            Assert.That(fixture.ViewModel.BusyProgressValue, Is.EqualTo(1));
        });

        fixture.Drafts.Gate.SetResult();
        fixture.PumpUntil(() => !fixture.ViewModel.IsBusy);
    }

    [AvaloniaTest]
    public void AfterDrafting_StatusReportsAppliedCount()
    {
        var fixture = OpenWithPendingEdit();

        fixture.ViewModel.DraftMissingAndStaleCommand.Execute(null);
        fixture.PumpUntil(() => !fixture.ViewModel.IsBusy && fixture.ViewModel.StatusText.StartsWith("Drafted"));

        Assert.That(fixture.ViewModel.StatusText, Is.EqualTo("Drafted 1 of 1 item(s)."));
        Assert.That(fixture.Translation("es").Text, Is.EqualTo("Hola"));
    }

    [AvaloniaTest]
    public void LateProgressReport_IsIgnoredOnceDraftingEnded()
    {
        var fixture = OpenWithPendingEdit();
        fixture.ViewModel.DraftMissingAndStaleCommand.Execute(null);
        fixture.PumpUntil(() => !fixture.ViewModel.IsBusy && fixture.ViewModel.StatusText.StartsWith("Drafted"));
        var statusBefore = fixture.ViewModel.StatusText;

        fixture.Drafts.LastProgress!.Report(new TranslationDraftProgress(3, 9));
        fixture.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.StatusText, Is.EqualTo(statusBefore));
            Assert.That(fixture.ViewModel.BusyProgressMaximum, Is.EqualTo(1));
        });
    }

    [AvaloniaTest]
    public void ProjectCommands_FollowTheProjectFolderBinding()
    {
        var folder = Path.Combine(Path.GetTempPath(), "game");
        var bound = MainViewModelFixture.Open(
            CatalogBuilder.WithEntries(["es"], "a"),
            new ProjectFolderBinding
            {
                CatalogId = "catalog",
                FolderPath = folder,
                ImportFileName = "UI.csv",
                ExportFileName = "UI.csv"
            });
        var unbound = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));

        Assert.Multiple(() =>
        {
            Assert.That(bound.ViewModel.HasProjectFolder, Is.True);
            Assert.That(bound.ViewModel.CanRunProjectCommands, Is.True);
            Assert.That(bound.ViewModel.ProjectFolderText, Is.EqualTo("Project: game"));
            Assert.That(bound.ViewModel.ProjectFolderToolTip, Does.Contain("Import: UI.csv"));
            Assert.That(unbound.ViewModel.HasProjectFolder, Is.False);
            Assert.That(unbound.ViewModel.CanRunProjectCommands, Is.False);
            Assert.That(unbound.ViewModel.ProjectFolderText, Is.EqualTo("No project folder"));
        });
    }

    private static MainViewModelFixture OpenWithPendingEdit()
    {
        var catalog = CatalogBuilder.WithEntries(["es"], "a", "b");
        var fixture = MainViewModelFixture.Open(
            catalog,
            new ProjectFolderBinding
            {
                CatalogId = "catalog",
                FolderPath = Path.Combine(Path.GetTempPath(), "game"),
                ImportFileName = "UI.csv",
                ExportFileName = "UI.csv"
            });
        fixture.Translation("es").Text = "Hola";
        return fixture;
    }
}
