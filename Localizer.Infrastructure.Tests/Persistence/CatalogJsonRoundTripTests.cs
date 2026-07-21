using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;
using Localizer.Core.Fingerprints;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;
using Localizer.Infrastructure.Persistence.Json;

namespace Localizer.Infrastructure.Tests.Persistence;

public sealed class CatalogJsonMapperTests
{
    private readonly CatalogJsonMapper _mapper = new();

    [Test]
    public void ToDocument_ToDomain_RoundTripsSupportedFields()
    {
        var original = CatalogTestData.CreateSampleCatalog();
        var document = _mapper.ToDocument(original);
        var restored = _mapper.ToDomain(document);

        CatalogTestData.AssertCatalogsEquivalent(original, restored);
    }

    [Test]
    public void ToDomain_RejectsUnsupportedSchemaVersion()
    {
        var document = _mapper.ToDocument(CatalogTestData.CreateSampleCatalog());
        document.SchemaVersion = 99;

        var exception = Assert.Throws<CatalogPersistenceException>(() => _mapper.ToDomain(document));

        Assert.That(exception!.Code, Is.EqualTo(CatalogPersistenceErrorCodes.UnsupportedVersion));
    }

    [Test]
    public void ToDomain_RejectsDuplicateEntryKeys()
    {
        var document = _mapper.ToDocument(CatalogTestData.CreateSampleCatalog());
        document.Entries.Add(new CatalogEntryDto
        {
            Key = document.Entries[0].Key,
            SourceText = document.Entries[0].SourceText
        });

        var exception = Assert.Throws<CatalogPersistenceException>(() => _mapper.ToDomain(document));

        Assert.That(exception!.Code, Is.EqualTo(CatalogPersistenceErrorCodes.MapFailed));
    }

    [Test]
    public void ToDomain_RejectsInvalidFingerprint()
    {
        var document = _mapper.ToDocument(CatalogTestData.CreateSampleCatalog());
        document.Entries[0].Translations!["fi"].BasedOnFingerprint = "not-a-fingerprint";

        var exception = Assert.Throws<CatalogPersistenceException>(() => _mapper.ToDomain(document));

        Assert.That(exception!.Code, Is.EqualTo(CatalogPersistenceErrorCodes.MapFailed));
    }

    [Test]
    public void ToDocument_OrdersEntriesAndLocalesDeterministically()
    {
        var catalog = new Catalog(
            1,
            CatalogId.Create("ordering-test"),
            Locale.Create("en"),
            [Locale.Create("fi"), Locale.Create("es")],
            MessageSyntaxProfile.Composite);

        catalog.AddEntry(CreateEntry("z_last", "Z"));
        catalog.AddEntry(CreateEntry("a_first", "A"));

        var document = _mapper.ToDocument(catalog);

        Assert.That(document.Entries.Select(entry => entry.Key), Is.EqualTo(["a_first", "z_last"]));
    }

    [Test]
    public void ToDocument_ToDomain_PreservesNonAsciiText()
    {
        var catalog = new Catalog(
            1,
            CatalogId.Create("unicode-test"),
            Locale.Create("en"),
            [Locale.Create("fi")],
            MessageSyntaxProfile.Plain);

        var fingerprint = Fingerprint.Create("placeholder");
        catalog.AddEntry(new CatalogEntry
        {
            Key = EntryKey.Create("greeting"),
            SourceText = "Hello",
            Translations = new Dictionary<Locale, Translation>
            {
                [Locale.Create("fi")] = new()
                {
                    Text = "Tervetuloa \u00E4\u00F6\u00E5",
                    State = TranslationState.Draft,
                    BasedOnFingerprint = fingerprint,
                    Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
                }
            }
        });

        var restored = _mapper.ToDomain(_mapper.ToDocument(catalog));

        Assert.That(
            restored.Entries.Values.Single().Translations[Locale.Create("fi")].Text,
            Is.EqualTo("Tervetuloa \u00E4\u00F6\u00E5"));
    }

    private static CatalogEntry CreateEntry(string key, string sourceText) =>
        new()
        {
            Key = EntryKey.Create(key),
            SourceText = sourceText,
            Translations = new Dictionary<Locale, Translation>()
        };
}
