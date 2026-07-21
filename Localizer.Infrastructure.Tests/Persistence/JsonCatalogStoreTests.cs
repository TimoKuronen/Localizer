using System.Text;
using System.Text.Json;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;
using Localizer.Core.Fingerprints;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;
using Localizer.Infrastructure.Persistence.Json;

namespace Localizer.Infrastructure.Tests.Persistence;

public sealed class JsonCatalogStoreTests
{
    private readonly JsonCatalogStore _store = new();
    private readonly CatalogJsonMapper _mapper = new();

    [Test]
    public async Task SaveAsync_LoadAsync_RoundTripsCatalog()
    {
        var catalog = CatalogTestData.CreateSampleCatalog();
        var path = CreateTempCatalogPath();

        await _store.SaveAsync(path, catalog);
        var restored = await _store.LoadAsync(path);

        CatalogTestData.AssertCatalogsEquivalent(catalog, restored);
    }

    [Test]
    public async Task SaveAsync_WritesUtf8WithoutByteOrderMarkAndTrailingNewline()
    {
        var path = CreateTempCatalogPath();
        await _store.SaveAsync(path, CatalogTestData.CreateSampleCatalog());

        var bytes = await File.ReadAllBytesAsync(path);

        Assert.That(bytes.Length, Is.GreaterThan(0));
        Assert.That(bytes[0], Is.Not.EqualTo(0xEF));
        Assert.That(bytes[^1], Is.EqualTo((byte)'\n'));
    }

    [Test]
    public async Task LoadAsync_RejectsByteOrderMark()
    {
        var path = CreateTempCatalogPath();
        var json = JsonSerializer.Serialize(
            _mapper.ToDocument(CatalogTestData.CreateSampleCatalog()),
            CatalogJsonSerializerOptions.Create());
        var jsonBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
        var bytes = new byte[jsonBytes.Length + 3];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        Array.Copy(jsonBytes, 0, bytes, 3, jsonBytes.Length);
        await File.WriteAllBytesAsync(path, bytes);

        var exception = Assert.ThrowsAsync<CatalogPersistenceException>(() => _store.LoadAsync(path));

        Assert.That(exception!.Code, Is.EqualTo(CatalogPersistenceErrorCodes.ByteOrderMarkPresent));
    }

    [Test]
    public async Task LoadAsync_RejectsMalformedJson()
    {
        var path = CreateTempCatalogPath();
        await File.WriteAllTextAsync(path, "{ not valid json }\n", new UTF8Encoding(false));

        var exception = Assert.ThrowsAsync<CatalogPersistenceException>(() => _store.LoadAsync(path));

        Assert.That(exception!.Code, Is.EqualTo(CatalogPersistenceErrorCodes.Malformed));
    }

    [Test, Explicit("Regenerates the committed golden fixture.")]
    public async Task GenerateGoldenFixture()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..",
            "Fixtures",
            "sample-catalog.v1.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
        await _store.SaveAsync(fixturePath, CatalogTestData.CreateSampleCatalog());
    }

    [Test]
    public async Task SaveAsync_ProducesGoldenBytes()
    {
        var path = CreateTempCatalogPath();
        await _store.SaveAsync(path, CatalogTestData.CreateSampleCatalog());

        var expectedPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Fixtures",
            "sample-catalog.v1.json");
        var expectedBytes = await File.ReadAllBytesAsync(expectedPath);
        var actualBytes = await File.ReadAllBytesAsync(path);

        Assert.That(actualBytes, Is.EqualTo(expectedBytes));
    }

    [Test]
    public async Task LoadAsync_LoadsGoldenFixture()
    {
        var fixturePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Fixtures",
            "sample-catalog.v1.json");

        var catalog = await _store.LoadAsync(fixturePath);

        Assert.That(catalog.CatalogId.Value, Is.EqualTo("runner-game"));
        Assert.That(catalog.Entries.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task SaveAsync_DoesNotCorruptExistingCatalogWhenTempWriteFails()
    {
        var path = CreateTempCatalogPath();
        await _store.SaveAsync(path, CatalogTestData.CreateSampleCatalog());
        var originalBytes = await File.ReadAllBytesAsync(path);

        Directory.CreateDirectory(path + ".tmp");

        var exception = Assert.ThrowsAsync<CatalogPersistenceException>(
            () => _store.SaveAsync(path, CatalogTestData.CreateAlternateCatalog()));

        Assert.That(exception!.Code, Is.EqualTo(CatalogPersistenceErrorCodes.IoFailed));
        Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(originalBytes));
    }

    private static string CreateTempCatalogPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "localizer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "catalog.json");
    }
}

internal static class CatalogTestData
{
    public static Catalog CreateSampleCatalog()
    {
        var catalog = new Catalog(
            1,
            CatalogId.Create("runner-game"),
            Locale.Create("en"),
            [Locale.Create("es"), Locale.Create("fi")],
            MessageSyntaxProfile.Composite);

        var fingerprint = Fingerprint.Create("sample-fingerprint-input");
        catalog.AddEntry(new CatalogEntry
        {
            Key = EntryKey.Create("menu_start_button"),
            Category = "ui",
            SourceText = "Start",
            DeveloperNotes = "Main menu button. Use a short verb.",
            Context = new TranslationContext
            {
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["intent"] = "begin a new run",
                    ["surface"] = "main-menu"
                }
            },
            Constraints = new EntryConstraints { MaxGraphemes = 12 },
            Translations = new Dictionary<Locale, Translation>
            {
                [Locale.Create("fi")] = new()
                {
                    Text = "Aloita",
                    State = TranslationState.Approved,
                    BasedOnFingerprint = fingerprint,
                    Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
                },
                [Locale.Create("es")] = new()
                {
                    Text = "Empezar",
                    State = TranslationState.Draft,
                    BasedOnFingerprint = fingerprint,
                    Provenance = new TranslationProvenance
                    {
                        Origin = TranslationOrigin.Model,
                        ProviderName = "local-http",
                        ModelName = "test-model",
                        GeneratedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)
                    }
                }
            }
        });

        catalog.AddEntry(new CatalogEntry
        {
            Key = EntryKey.Create("score_label"),
            SourceText = "Score: {0}",
            Translations = new Dictionary<Locale, Translation>()
        });

        return catalog;
    }

    public static Catalog CreateAlternateCatalog()
    {
        var catalog = new Catalog(
            1,
            CatalogId.Create("alternate"),
            Locale.Create("en"),
            [Locale.Create("fi")],
            MessageSyntaxProfile.Plain);

        catalog.AddEntry(new CatalogEntry
        {
            Key = EntryKey.Create("only_entry"),
            SourceText = "Alternate",
            Translations = new Dictionary<Locale, Translation>()
        });

        return catalog;
    }

    public static void AssertCatalogsEquivalent(Catalog expected, Catalog actual)
    {
        Assert.That(actual.SchemaVersion, Is.EqualTo(expected.SchemaVersion));
        Assert.That(actual.CatalogId.Value, Is.EqualTo(expected.CatalogId.Value));
        Assert.That(actual.SourceLocale.Value, Is.EqualTo(expected.SourceLocale.Value));
        Assert.That(
            actual.RequiredLocales.Select(locale => locale.Value),
            Is.EqualTo(expected.RequiredLocales.Select(locale => locale.Value)));
        Assert.That(actual.DefaultSyntaxProfile, Is.EqualTo(expected.DefaultSyntaxProfile));
        Assert.That(actual.Entries.Count, Is.EqualTo(expected.Entries.Count));

        foreach (var expectedEntry in expected.Entries.Values.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal))
        {
            var actualEntry = actual.Entries[expectedEntry.Key];
            Assert.That(actualEntry.Category, Is.EqualTo(expectedEntry.Category));
            Assert.That(actualEntry.SourceText, Is.EqualTo(expectedEntry.SourceText));
            Assert.That(actualEntry.AllowEmptyText, Is.EqualTo(expectedEntry.AllowEmptyText));
            Assert.That(actualEntry.DeveloperNotes, Is.EqualTo(expectedEntry.DeveloperNotes));
            Assert.That(actualEntry.SyntaxProfileOverride, Is.EqualTo(expectedEntry.SyntaxProfileOverride));
            Assert.That(actualEntry.Context.Values, Is.EquivalentTo(expectedEntry.Context.Values));
            Assert.That(actualEntry.ExternalIds.Values, Is.EquivalentTo(expectedEntry.ExternalIds.Values));
            Assert.That(actualEntry.Constraints.MaxGraphemes, Is.EqualTo(expectedEntry.Constraints.MaxGraphemes));
            Assert.That(actualEntry.Constraints.MaxUtf8Bytes, Is.EqualTo(expectedEntry.Constraints.MaxUtf8Bytes));
            Assert.That(actualEntry.Constraints.MaxLines, Is.EqualTo(expectedEntry.Constraints.MaxLines));
            Assert.That(actualEntry.Constraints.RequiredTerms, Is.EquivalentTo(expectedEntry.Constraints.RequiredTerms));
            Assert.That(actualEntry.Constraints.ForbiddenTerms, Is.EquivalentTo(expectedEntry.Constraints.ForbiddenTerms));
            Assert.That(actualEntry.Translations.Count, Is.EqualTo(expectedEntry.Translations.Count));

            foreach (var expectedTranslation in expectedEntry.Translations)
            {
                var actualTranslation = actualEntry.Translations[expectedTranslation.Key];
                Assert.That(actualTranslation.Text, Is.EqualTo(expectedTranslation.Value.Text));
                Assert.That(actualTranslation.State, Is.EqualTo(expectedTranslation.Value.State));
                Assert.That(
                    actualTranslation.BasedOnFingerprint,
                    Is.EqualTo(expectedTranslation.Value.BasedOnFingerprint));
                Assert.That(
                    actualTranslation.Provenance.Origin,
                    Is.EqualTo(expectedTranslation.Value.Provenance.Origin));
                Assert.That(
                    actualTranslation.Provenance.ProviderName,
                    Is.EqualTo(expectedTranslation.Value.Provenance.ProviderName));
                Assert.That(
                    actualTranslation.Provenance.ModelName,
                    Is.EqualTo(expectedTranslation.Value.Provenance.ModelName));
                Assert.That(
                    actualTranslation.Provenance.GeneratedAtUtc,
                    Is.EqualTo(expectedTranslation.Value.Provenance.GeneratedAtUtc));
            }
        }
    }
}
