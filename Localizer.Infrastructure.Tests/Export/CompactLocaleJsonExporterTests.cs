using System.Text;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;
using Localizer.Infrastructure.Export;

namespace Localizer.Infrastructure.Tests.Export;

public sealed class CompactLocaleJsonExporterTests
{
    [Test]
    public async Task ExportAsync_WritesCompactUtf8LocaleFilesInOrdinalKeyOrder()
    {
        var catalog = CreateExportableCatalog();
        var outputDirectory = CreateTempDirectory();
        var exporter = new CompactLocaleJsonExporter();

        var written = await exporter.ExportAsync(outputDirectory, catalog);

        Assert.That(written, Is.EqualTo(new[]
        {
            Path.Combine(outputDirectory, "es.json"),
            Path.Combine(outputDirectory, "fi.json")
        }));

        var esBytes = await File.ReadAllBytesAsync(Path.Combine(outputDirectory, "es.json"));
        var fiBytes = await File.ReadAllBytesAsync(Path.Combine(outputDirectory, "fi.json"));

        Assert.That(esBytes[0], Is.Not.EqualTo(0xEF));
        Assert.That(esBytes[^1], Is.EqualTo((byte)'\n'));
        Assert.That(fiBytes[^1], Is.EqualTo((byte)'\n'));

        var esJson = Encoding.UTF8.GetString(esBytes);
        var fiJson = Encoding.UTF8.GetString(fiBytes);

        Assert.That(esJson, Is.EqualTo("{\"menu_start_button\":\"Empezar\",\"score_label\":\"Puntos: {0}\"}\n"));
        Assert.That(fiJson, Is.EqualTo("{\"menu_start_button\":\"Aloita\",\"score_label\":\"Pisteet: {0}\"}\n"));
    }

    [Test]
    public async Task ExportAsync_OmitsNonApprovedEntriesDefensively()
    {
        var catalog = CreateExportableCatalog();
        var draftEntry = catalog.Entries[EntryKey.Create("menu_start_button")];
        var translations = new Dictionary<Locale, Translation>(draftEntry.Translations)
        {
            [Locale.Create("es")] = draftEntry.Translations[Locale.Create("es")] with
            {
                State = TranslationState.Draft
            }
        };
        catalog.ReplaceEntry(draftEntry with { Translations = translations });

        var outputDirectory = CreateTempDirectory();
        await new CompactLocaleJsonExporter().ExportAsync(outputDirectory, catalog);

        var esJson = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "es.json"), Encoding.UTF8);
        Assert.That(esJson, Is.EqualTo("{\"score_label\":\"Puntos: {0}\"}\n"));
    }

    private static Catalog CreateExportableCatalog()
    {
        var catalog = new Catalog(
            1,
            CatalogId.Create("runner-game"),
            Locale.Create("en"),
            [Locale.Create("es"), Locale.Create("fi")],
            MessageSyntaxProfile.Composite);

        AddApprovedEntry(catalog, "score_label", "Score: {0}", "es", "Puntos: {0}");
        AddApprovedEntry(catalog, "menu_start_button", "Start", "es", "Empezar");
        AddApprovedLocale(catalog, "score_label", "fi", "Pisteet: {0}");
        AddApprovedLocale(catalog, "menu_start_button", "fi", "Aloita");

        return catalog;
    }

    private static void AddApprovedEntry(
        Catalog catalog,
        string key,
        string sourceText,
        string locale,
        string translationText)
    {
        var entryKey = EntryKey.Create(key);
        var skeleton = new CatalogEntry
        {
            Key = entryKey,
            SourceText = sourceText
        };
        catalog.AddEntry(skeleton with
        {
            Translations = new Dictionary<Locale, Translation>
            {
                [Locale.Create(locale)] = new Translation
                {
                    Text = translationText,
                    State = TranslationState.Approved,
                    BasedOnFingerprint = catalog.GetCurrentFingerprint(skeleton),
                    Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
                }
            }
        });
    }

    private static void AddApprovedLocale(
        Catalog catalog,
        string key,
        string locale,
        string translationText)
    {
        var entry = catalog.Entries[EntryKey.Create(key)];
        var translations = new Dictionary<Locale, Translation>(entry.Translations)
        {
            [Locale.Create(locale)] = new Translation
            {
                Text = translationText,
                State = TranslationState.Approved,
                BasedOnFingerprint = catalog.GetCurrentFingerprint(entry),
                Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
            }
        };
        catalog.ReplaceEntry(entry with { Translations = translations });
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "localizer-export-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
