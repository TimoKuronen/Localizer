using Avalonia.Headless.NUnit;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class MainViewModelSourceStringsImportTests
{
    [AvaloniaTest]
    public void ImportSourceStrings_AddsEntriesMarksDirtyAndReportsCounts()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Dialogs.SourceStringsPath = WriteSourceFile(
            """{"schemaVersion":1,"sourceLocale":"en","entries":[{"key":"b","sourceText":"B"},{"key":"c","sourceText":"C"}]}""");
        fixture.ViewModel.IsDirty = false;

        fixture.ViewModel.ImportSourceStringsCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.IsDirty);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.Entries.Select(entry => entry.Key), Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(fixture.ViewModel.StatusText, Does.Contain("Added 2, updated 0, unchanged 0."));
        });
    }

    [AvaloniaTest]
    public void ImportSourceStrings_ShowsAMessageAndLeavesTheCatalogCleanOnFailure()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Dialogs.SourceStringsPath = WriteSourceFile("not json");
        fixture.ViewModel.IsDirty = false;

        fixture.ViewModel.ImportSourceStringsCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.Messages.Count > 0);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.Messages[0], Does.Contain("catalog.json.malformed"));
            Assert.That(fixture.ViewModel.IsDirty, Is.False);
            Assert.That(fixture.ViewModel.Entries, Has.Count.EqualTo(1));
        });
    }

    [AvaloniaTest]
    public void ImportSourceStrings_DoesNothingWhenThePickerIsCancelled()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Dialogs.SourceStringsPath = null;
        fixture.ViewModel.IsDirty = false;

        fixture.ViewModel.ImportSourceStringsCommand.Execute(null);
        fixture.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.IsDirty, Is.False);
            Assert.That(fixture.Dialogs.Messages, Is.Empty);
        });
    }

    private static string WriteSourceFile(string json)
    {
        var directory = Path.Combine(Path.GetTempPath(), "localizer-desktop-source-strings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "source.json");
        File.WriteAllText(path, json);
        return path;
    }
}
