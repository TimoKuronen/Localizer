using Avalonia.Headless.NUnit;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class MainViewModelSourceStringsWorkflowTests
{
    [AvaloniaTest]
    public void ChangedSourceTextInTheFile_MarksTheApprovedTranslationStale()
    {
        var catalog = CatalogWithApproved("settings.title", "Settings", "Ajustes");
        var fixture = MainViewModelFixture.Open(catalog);
        fixture.Dialogs.SourceStringsPath = WriteFile(
            """{"schemaVersion":1,"sourceLocale":"en","entries":[{"key":"settings.title","sourceText":"Options"}]}""");

        fixture.ViewModel.ImportSourceStringsCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.StatusText.StartsWith("Imported"));

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.StatusText, Does.Contain("Added 0, updated 1, unchanged 0."));
            Assert.That(fixture.ViewModel.Entries.Single().LocaleStatuses, Does.Contain("es:Stale"));
        });
    }

    [AvaloniaTest]
    public void ChangedDeveloperNotes_MarkOnlyTheAffectedEntryStale()
    {
        var catalog = CatalogWithApproved("settings.title", "Settings", "Ajustes");
        CatalogBuilder.AddEntry(catalog, "ui.quit", "Quit");
        CatalogBuilder.AddDraft(catalog, "ui.quit", "es", "Salir");
        Approve(catalog, "ui.quit");
        var fixture = MainViewModelFixture.Open(catalog);
        fixture.Dialogs.SourceStringsPath = WriteFile(
            """
            {"schemaVersion":1,"sourceLocale":"en","entries":[
              {"key":"settings.title","sourceText":"Settings","developerNotes":"Window heading."},
              {"key":"ui.quit","sourceText":"Quit"}]}
            """);

        fixture.ViewModel.ImportSourceStringsCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.StatusText.StartsWith("Imported"));

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.StatusText, Does.Contain("updated 1, unchanged 1."));
            Assert.That(StatusOf(fixture, "settings.title"), Does.Contain("es:Stale"));
            Assert.That(StatusOf(fixture, "ui.quit"), Does.Contain("es:Approved"));
        });
    }

    [AvaloniaTest]
    public void SourceLocaleMismatch_ShowsTheErrorCodeAndLeavesTheCatalogUntouched()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Dialogs.SourceStringsPath = WriteFile(
            """{"schemaVersion":1,"sourceLocale":"fi","entries":[{"key":"b","sourceText":"B"}]}""");
        fixture.ViewModel.IsDirty = false;

        fixture.ViewModel.ImportSourceStringsCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.Messages.Count > 0);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.Messages[0], Does.Contain("import.locale_mismatch"));
            Assert.That(fixture.ViewModel.IsDirty, Is.False);
            Assert.That(fixture.ViewModel.Entries, Has.Count.EqualTo(1));
        });
    }

    [AvaloniaTest]
    public void MisspelledProperty_IsRejectedAndLeavesTheCatalogUntouched()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Dialogs.SourceStringsPath = WriteFile(
            """{"schemaVersion":1,"sourceLocale":"en","entries":[{"key":"b","sourceText":"B","developerNote":"typo"}]}""");
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
    public void RuntimeExport_IsBlockedUntilEveryEntryIsApproved_ThenWritesTheLocaleFile()
    {
        var catalog = CatalogBuilder.WithEntries(["es"], "settings.title", "ui.quit");
        CatalogBuilder.AddDraft(catalog, "settings.title", "es", "Ajustes");
        Approve(catalog, "settings.title");
        var fixture = MainViewModelFixture.Open(catalog);
        var outputDirectory = Path.Combine(NewDirectory(), "out");
        Directory.CreateDirectory(outputDirectory);
        fixture.Dialogs.ExportDirectory = outputDirectory;

        fixture.ViewModel.ExportCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.Messages.Count > 0);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.Messages[0], Does.StartWith("Export failed"));
            Assert.That(fixture.ViewModel.StatusText, Is.EqualTo("Export blocked."));
            Assert.That(File.Exists(Path.Combine(outputDirectory, "es.json")), Is.False);
        });

        CatalogBuilder.AddDraft(catalog, "ui.quit", "es", "Salir");
        Approve(catalog, "ui.quit");
        fixture.Dialogs.Messages.Clear();

        fixture.ViewModel.ExportCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.Messages.Count > 0);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.Messages[0], Does.StartWith("Export complete"));
            Assert.That(
                File.ReadAllText(Path.Combine(outputDirectory, "es.json")),
                Is.EqualTo("{\"settings.title\":\"Ajustes\",\"ui.quit\":\"Salir\"}\n"));
        });
    }

    private static string StatusOf(MainViewModelFixture fixture, string key) =>
        fixture.ViewModel.Entries.Single(entry => entry.Key == key).LocaleStatuses;

    private static Catalog CatalogWithApproved(string key, string source, string translation)
    {
        var catalog = CatalogBuilder.Create(["es"]);
        CatalogBuilder.AddEntry(catalog, key, source);
        CatalogBuilder.AddDraft(catalog, key, "es", translation);
        Approve(catalog, key);
        return catalog;
    }

    private static void Approve(Catalog catalog, string key)
    {
        var approved = new ApproveTranslationUseCase().Execute(
            catalog,
            new ApproveTranslationRequest { Key = key, Locale = "es" });
        Assert.That(approved.Succeeded, Is.True, approved.ErrorMessage);
    }

    private static string WriteFile(string json)
    {
        var path = Path.Combine(NewDirectory(), "source.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string NewDirectory() =>
        Directory.CreateTempSubdirectory("localizer-source-strings-workflow-").FullName;
}
