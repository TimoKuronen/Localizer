using Avalonia.Headless.NUnit;
using Localizer.Application.Project;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class MainViewModelSourceCleanupTests
{
    private const string SourceWithOnlyA =
        """{"schemaVersion":1,"sourceLocale":"en","entries":[{"key":"a","sourceText":"A"}]}""";

    [AvaloniaTest]
    public void RemovesTheTickedEntriesAndMarksTheCatalogDirty()
    {
        var fixture = OpenWithSource(SourceWithOnlyA);
        fixture.Dialogs.ChecklistAnswer = _ => ["b"];

        fixture.ViewModel.RemoveEntriesNotInSourceCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.IsDirty);

        var checklist = fixture.Dialogs.Checklists.Single();
        Assert.Multiple(() =>
        {
            Assert.That(checklist.Items.Select(item => item.Id), Is.EqualTo(new[] { "b", "c" }));
            Assert.That(checklist.Items.All(item => item.IsSelected), Is.True);
            Assert.That(checklist.ConfirmIsDefault, Is.False);
            Assert.That(fixture.ViewModel.Entries.Select(entry => entry.Key), Is.EqualTo(new[] { "a", "c" }));
        });
    }

    [AvaloniaTest]
    public void CancellingKeepsEveryEntry()
    {
        var fixture = OpenWithSource(SourceWithOnlyA);

        fixture.ViewModel.RemoveEntriesNotInSourceCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.Checklists.Count > 0 && !fixture.ViewModel.IsBusy);

        Assert.That(fixture.ViewModel.Entries, Has.Count.EqualTo(3));
        Assert.That(fixture.ViewModel.IsDirty, Is.False);
    }

    [AvaloniaTest]
    public void AMissingProjectFileIsAnErrorRatherThanAPrompt()
    {
        var binding = new ProjectFolderBinding
        {
            CatalogId = "catalog",
            FolderPath = Path.Combine(Path.GetTempPath(), "localizer-missing-project"),
            ImportFileName = "UI.csv",
            ExportFileName = "UI.csv"
        };
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"), binding);

        fixture.ViewModel.RemoveEntriesNotInSourceCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.Messages.Count > 0);

        Assert.That(fixture.Dialogs.ComparisonPickCount, Is.Zero);
        Assert.That(fixture.Dialogs.Checklists, Is.Empty);
    }

    private static MainViewModelFixture OpenWithSource(string json)
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a", "b", "c"));
        var path = Path.Combine(Directory.CreateTempSubdirectory("localizer-desktop-tests-").FullName, "source.json");
        File.WriteAllText(path, json);
        fixture.Dialogs.ComparisonSourcePath = path;
        return fixture;
    }
}
