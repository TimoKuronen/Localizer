using Localizer.Application.Import;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;
using Localizer.Infrastructure.Export;
using Localizer.Infrastructure.Import;

namespace Localizer.Infrastructure.Tests.Import;

public sealed class UnityCsvParserTests
{
    [Test]
    public async Task Parse_ReadsUnityStringTableSampleFixture()
    {
        var document = await new UnityCsvReader().ReadAsync(GetFixturePath("unity-string-table-sample.csv"));

        Assert.That(document.SourceLocale, Is.EqualTo("en"));
        Assert.That(document.TargetLocales, Is.EqualTo(new[] { "es" }));
        Assert.That(document.Rows.Select(row => row.Key), Is.EqualTo(new[]
        {
            "ui.lose.title",
            "ui.menu.play",
            "ui.win.coins_earned",
            "ui.win.next_level",
            "ui.win.title"
        }));
        Assert.That(document.Rows.Single(row => row.Key == "ui.win.coins_earned").SourceText, Is.EqualTo("+{0} coins earned"));
        Assert.That(document.Rows.Single(row => row.Key == "ui.win.title").UnityId, Is.EqualTo("10547617792"));
    }

    [Test]
    public async Task Parse_RejectsDuplicateKeys()
    {
        const string csv = """
            Key,Id,English(en),Spanish(es)
            ui.start,1,Start,
            ui.start,2,Begin,
            """;

        var path = Path.Combine(CreateTempDirectory(), "duplicate-keys.csv");
        await File.WriteAllTextAsync(path, csv);

        var exception = Assert.ThrowsAsync<Application.Persistence.CatalogPersistenceException>(
            () => new UnityCsvReader().ReadAsync(path));

        Assert.That(exception!.Message, Does.Contain("duplicate key"));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "localizer-unity-csv-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", fileName);
}

public sealed class UnityCsvBridgeIntegrationTests
{
    [Test]
    public async Task ImportAndExport_RoundTripPreservesKeysSourceAndUnityIds()
    {
        var catalog = CreateUnityBridgeCatalog();
        var tempDirectory = CreateTempDirectory();
        var csvPath = Path.Combine(tempDirectory, "unity-string-table-sample.csv");
        File.Copy(GetFixturePath("unity-string-table-sample.csv"), csvPath, overwrite: true);

        var importOutcome = await new ImportUnityCsvUseCase(new UnityCsvReader(), new MergeUnityCsvImportUseCase())
            .ExecuteAsync(catalog, csvPath);

        Assert.That(importOutcome.Succeeded, Is.True);
        Assert.That(importOutcome.MergeOutcome.AddedCount, Is.EqualTo(5));

        var coinsEntry = catalog.Entries[EntryKey.Create("ui.win.coins_earned")];
        Assert.That(coinsEntry.SyntaxProfileOverride, Is.EqualTo(MessageSyntaxProfile.Composite));
        Assert.That(
            coinsEntry.ExternalIds.Values[UnityCsvBridgeConstants.UnityExternalIdNamespace],
            Is.EqualTo("11222900737"));

        new SetTranslationDraftUseCase().Execute(catalog, new SetTranslationDraftRequest
        {
            Key = "ui.menu.play",
            Locale = "es",
            Text = "Jugar"
        });
        new ApproveTranslationUseCase().Execute(catalog, new ApproveTranslationRequest
        {
            Key = "ui.menu.play",
            Locale = "es"
        });

        var exportPath = Path.Combine(tempDirectory, "ui-export.csv");
        var exportOutcome = await new ExportUnityCsvUseCase(new UnityCsvExporter()).ExecuteAsync(catalog, exportPath);

        Assert.That(exportOutcome.Succeeded, Is.True);

        var exported = await new UnityCsvReader().ReadAsync(exportPath);
        var playRow = exported.Rows.Single(row => row.Key == "ui.menu.play");
        var titleRow = exported.Rows.Single(row => row.Key == "ui.win.title");

        Assert.That(playRow.SourceText, Is.EqualTo("Play"));
        Assert.That(titleRow.UnityId, Is.EqualTo("10547617792"));
    }

    [Test]
    public async Task Export_WritesUtf8WithoutBomAndApprovedCellsOnly()
    {
        var catalog = CreateUnityBridgeCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.menu.play",
            SourceText = "Play"
        });

        var entry = catalog.Entries[EntryKey.Create("ui.menu.play")];
        catalog.ReplaceEntry(entry with
        {
            ExternalIds = new ExternalIds
            {
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [UnityCsvBridgeConstants.UnityExternalIdNamespace] = "11235483652"
                }
            }
        });

        new SetTranslationDraftUseCase().Execute(catalog, new SetTranslationDraftRequest
        {
            Key = "ui.menu.play",
            Locale = "es",
            Text = "Jugar"
        });
        new ApproveTranslationUseCase().Execute(catalog, new ApproveTranslationRequest
        {
            Key = "ui.menu.play",
            Locale = "es"
        });

        var exportPath = Path.Combine(CreateTempDirectory(), "ui-export.csv");
        await new UnityCsvExporter().ExportAsync(exportPath, catalog);

        var bytes = await File.ReadAllBytesAsync(exportPath);
        var csv = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.That(bytes[0], Is.Not.EqualTo(0xEF));
        Assert.That(bytes[^1], Is.EqualTo((byte)'\n'));
        Assert.That(csv, Does.StartWith("Key,Id,English(en),Spanish(es)"));
        Assert.That(csv, Does.Contain("ui.menu.play,11235483652,Play,Jugar"));
    }

    [Test]
    public void Merge_UpdatesSourceTextWithoutRemovingApprovedTranslations()
    {
        var catalog = CreateUnityBridgeCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.menu.play",
            SourceText = "Play"
        });

        var entry = catalog.Entries[EntryKey.Create("ui.menu.play")];
        var fingerprint = catalog.GetCurrentFingerprint(entry);
        catalog.ReplaceEntry(entry with
        {
            Translations = new Dictionary<Locale, Translation>
            {
                [Locale.Create("es")] = new Translation
                {
                    Text = "Jugar",
                    State = TranslationState.Approved,
                    BasedOnFingerprint = fingerprint,
                    Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
                }
            }
        });

        var mergeOutcome = new MergeUnityCsvImportUseCase().Execute(
            catalog,
            new UnityCsvDocument
            {
                SourceLocale = "en",
                TargetLocales = ["es"],
                Rows =
                [
                    new UnityCsvRow
                    {
                        Key = "ui.menu.play",
                        UnityId = "11235483652",
                        SourceText = "Play now"
                    }
                ]
            });

        var updated = catalog.Entries[EntryKey.Create("ui.menu.play")];

        Assert.That(mergeOutcome.Succeeded, Is.True);
        Assert.That(mergeOutcome.UpdatedCount, Is.EqualTo(1));
        Assert.That(updated.SourceText, Is.EqualTo("Play now"));
        Assert.That(updated.Translations[Locale.Create("es")].Text, Is.EqualTo("Jugar"));
        Assert.That(catalog.GetEffectiveStatus(updated, Locale.Create("es")), Is.EqualTo(TranslationEffectiveStatus.Stale));
    }

    private static Catalog CreateUnityBridgeCatalog() =>
        new(
            schemaVersion: 1,
            catalogId: CatalogId.Create("sample-game"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("es")],
            defaultSyntaxProfile: MessageSyntaxProfile.Composite);

    private static string GetFixturePath(string fileName) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", fileName);

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "localizer-unity-csv-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
