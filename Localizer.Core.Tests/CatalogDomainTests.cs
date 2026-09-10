using Localizer.Core.Catalogs;
using Localizer.Core.Fingerprints;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;

namespace Localizer.Core.Tests;

public sealed class EntryKeyTests
{
    [Test]
    public void Create_AcceptsValidKey()
    {
        var key = EntryKey.Create("menu_start_button");

        Assert.That(key.Value, Is.EqualTo("menu_start_button"));
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("menu start")]
    [TestCase("menu/start")]
    public void Create_RejectsInvalidKey(string value)
    {
        Assert.Throws<Errors.DomainValidationException>(() => EntryKey.Create(value));
    }
}

public sealed class LocaleTests
{
    [TestCase("en", "en")]
    [TestCase("EN", "en")]
    [TestCase("en-US", "en-US")]
    [TestCase("en_us", "en-US")]
    [TestCase("zh-Hans", "zh-Hans")]
    public void Create_NormalizesBcp47Tags(string input, string expected)
    {
        var locale = Locale.Create(input);

        Assert.That(locale.Value, Is.EqualTo(expected));
    }

    [Test]
    public void Create_RejectsEmptyLocale()
    {
        Assert.Throws<Errors.DomainValidationException>(() => Locale.Create("   "));
    }
}

public sealed class CatalogTests
{
    private static Catalog CreateCatalog() =>
        new(
            schemaVersion: 1,
            catalogId: CatalogId.Create("runner-game"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("fi"), Locale.Create("es")],
            defaultSyntaxProfile: MessageSyntaxProfile.Composite);

    private static CatalogEntry CreateEntry(
        string key,
        string sourceText,
        IReadOnlyDictionary<Locale, Translation>? translations = null,
        string? developerNotes = null,
        TranslationContext? context = null,
        EntryConstraints? constraints = null,
        MessageSyntaxProfile? syntaxOverride = null)
    {
        return new CatalogEntry
        {
            Key = EntryKey.Create(key),
            SourceText = sourceText,
            DeveloperNotes = developerNotes,
            Context = context ?? new TranslationContext(),
            Constraints = constraints ?? new EntryConstraints(),
            SyntaxProfileOverride = syntaxOverride,
            Translations = translations ?? new Dictionary<Locale, Translation>()
        };
    }

    [Test]
    public void AddEntry_RejectsDuplicateKeys()
    {
        var catalog = CreateCatalog();
        catalog.AddEntry(CreateEntry("menu_start", "Start"));

        Assert.Throws<Errors.DomainValidationException>(
            () => catalog.AddEntry(CreateEntry("menu_start", "Begin")));
    }

    [Test]
    public void AddEntry_RejectsCaseInsensitiveCollisions()
    {
        var catalog = CreateCatalog();
        catalog.AddEntry(CreateEntry("Menu_Start", "Start"));

        var exception = Assert.Throws<Errors.DomainValidationException>(
            () => catalog.AddEntry(CreateEntry("menu_start", "Start")));

        Assert.That(exception!.Code, Is.EqualTo(EntryKeyRules.CaseInsensitiveCollisionCode));
    }

    [Test]
    public void GetEffectiveStatus_ReturnsMissingWhenTranslationAbsent()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry("menu_start", "Start");
        catalog.AddEntry(entry);

        var status = catalog.GetEffectiveStatus(entry, Locale.Create("fi"));

        Assert.That(status, Is.EqualTo(TranslationEffectiveStatus.Missing));
    }

    [Test]
    public void GetEffectiveStatus_ReturnsDraftForCurrentFingerprint()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry("menu_start", "Start");
        catalog.AddEntry(entry);
        var fingerprint = catalog.GetCurrentFingerprint(entry);
        var translations = new Dictionary<Locale, Translation>
        {
            [Locale.Create("fi")] = new()
            {
                Text = "Aloita",
                State = TranslationState.Draft,
                BasedOnFingerprint = fingerprint,
                Provenance = new TranslationProvenance { Origin = TranslationOrigin.Model }
            }
        };
        entry = entry with { Translations = translations };

        var status = catalog.GetEffectiveStatus(entry, Locale.Create("fi"));

        Assert.That(status, Is.EqualTo(TranslationEffectiveStatus.Draft));
    }

    [Test]
    public void GetEffectiveStatus_ReturnsApprovedForCurrentApprovedTranslation()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry("menu_start", "Start");
        catalog.AddEntry(entry);
        var fingerprint = catalog.GetCurrentFingerprint(entry);
        var translations = new Dictionary<Locale, Translation>
        {
            [Locale.Create("fi")] = new()
            {
                Text = "Aloita",
                State = TranslationState.Approved,
                BasedOnFingerprint = fingerprint,
                Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
            }
        };
        entry = entry with { Translations = translations };

        var status = catalog.GetEffectiveStatus(entry, Locale.Create("fi"));

        Assert.That(status, Is.EqualTo(TranslationEffectiveStatus.Approved));
    }

    [Test]
    public void GetEffectiveStatus_ReturnsStaleWhenFingerprintDiffers()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry("menu_start", "Start");
        catalog.AddEntry(entry);
        var staleFingerprint = Fingerprint.Parse(
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var translations = new Dictionary<Locale, Translation>
        {
            [Locale.Create("fi")] = new()
            {
                Text = "Aloita",
                State = TranslationState.Approved,
                BasedOnFingerprint = staleFingerprint,
                Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
            }
        };
        entry = entry with { Translations = translations };

        var status = catalog.GetEffectiveStatus(entry, Locale.Create("fi"));

        Assert.That(status, Is.EqualTo(TranslationEffectiveStatus.Stale));
    }

    [Test]
    public void SourceTextChange_MarksApprovedTranslationStale()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry("menu_start", "Start");
        catalog.AddEntry(entry);
        var originalFingerprint = catalog.GetCurrentFingerprint(entry);
        var translations = new Dictionary<Locale, Translation>
        {
            [Locale.Create("fi")] = new()
            {
                Text = "Aloita",
                State = TranslationState.Approved,
                BasedOnFingerprint = originalFingerprint,
                Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
            }
        };
        entry = entry with { Translations = translations };

        var updatedEntry = entry with { SourceText = "Begin" };
        var status = catalog.GetEffectiveStatus(updatedEntry, Locale.Create("fi"));

        Assert.That(status, Is.EqualTo(TranslationEffectiveStatus.Stale));
        Assert.That(updatedEntry.Translations[Locale.Create("fi")].Text, Is.EqualTo("Aloita"));
    }

    [Test]
    public void OperationalMetadataChange_DoesNotChangeFingerprint()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry(
            "menu_start",
            "Start",
            translations: new Dictionary<Locale, Translation>
            {
                [Locale.Create("fi")] = new()
                {
                    Text = "Aloita",
                    State = TranslationState.Approved,
                    BasedOnFingerprint = Fingerprint.Create("placeholder"),
                    Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
                }
            },
            context: new TranslationContext
            {
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["surface"] = "main-menu"
                }
            });

        catalog.AddEntry(entry);
        var originalFingerprint = catalog.GetCurrentFingerprint(entry);

        var updatedEntry = entry with
        {
            Translations = new Dictionary<Locale, Translation>
            {
                [Locale.Create("fi")] = entry.Translations[Locale.Create("fi")] with
                {
                    Provenance = entry.Translations[Locale.Create("fi")].Provenance with
                    {
                        GeneratedAtUtc = DateTimeOffset.UtcNow
                    }
                }
            }
        };

        var updatedFingerprint = catalog.GetCurrentFingerprint(updatedEntry);

        Assert.That(updatedFingerprint, Is.EqualTo(originalFingerprint));
    }

    [Test]
    public void ContextChange_ChangesFingerprint()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry(
            "menu_start",
            "Start",
            context: new TranslationContext
            {
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["surface"] = "main-menu"
                }
            });
        catalog.AddEntry(entry);

        var originalFingerprint = catalog.GetCurrentFingerprint(entry);
        var updatedEntry = entry with
        {
            Context = new TranslationContext
            {
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["surface"] = "pause-menu"
                }
            }
        };
        var updatedFingerprint = catalog.GetCurrentFingerprint(updatedEntry);

        Assert.That(updatedFingerprint, Is.Not.EqualTo(originalFingerprint));
    }

    [Test]
    public void NewlineNormalization_DoesNotChangeFingerprint()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry("menu_start", "Line one\r\nLine two");
        catalog.AddEntry(entry);

        var originalFingerprint = catalog.GetCurrentFingerprint(entry);
        var updatedEntry = entry with { SourceText = "Line one\nLine two" };
        var updatedFingerprint = catalog.GetCurrentFingerprint(updatedEntry);

        Assert.That(updatedFingerprint, Is.EqualTo(originalFingerprint));
    }

    [Test]
    public void AddEntry_RejectsEmptySourceTextByDefault()
    {
        var catalog = CreateCatalog();

        Assert.Throws<Errors.DomainValidationException>(
            () => catalog.AddEntry(CreateEntry("menu_start", string.Empty)));
    }

    [Test]
    public void AddEntry_AllowsEmptySourceTextWhenExplicitlyEnabled()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry("menu_start", string.Empty) with { AllowEmptyText = true };

        Assert.DoesNotThrow(() => catalog.AddEntry(entry));
    }

    [Test]
    public void RequiredLocales_CannotIncludeSourceLocale()
    {
        Assert.Throws<Errors.DomainValidationException>(() =>
            new Catalog(
                1,
                CatalogId.Create("runner-game"),
                Locale.Create("en"),
                [Locale.Create("en"), Locale.Create("fi")],
                MessageSyntaxProfile.Plain));
    }

    [Test]
    public void ReplaceEntry_UpdatesExistingEntry()
    {
        var catalog = CreateCatalog();
        catalog.AddEntry(CreateEntry("menu_start", "Start"));

        catalog.ReplaceEntry(CreateEntry("menu_start", "Begin"));

        Assert.That(catalog.Entries[EntryKey.Create("menu_start")].SourceText, Is.EqualTo("Begin"));
    }

    [Test]
    public void ReplaceEntry_RejectsUnknownKey()
    {
        var catalog = CreateCatalog();

        var exception = Assert.Throws<Errors.DomainValidationException>(
            () => catalog.ReplaceEntry(CreateEntry("missing", "Text")));

        Assert.That(exception!.Code, Is.EqualTo("entry.not_found"));
    }

    [Test]
    public void ConfigureLocales_UpdatesSourceAndRequiredLocales()
    {
        var catalog = CreateCatalog();

        catalog.ConfigureLocales(Locale.Create("en"), [Locale.Create("de")]);

        Assert.That(catalog.SourceLocale.Value, Is.EqualTo("en"));
        Assert.That(catalog.RequiredLocales.Select(locale => locale.Value), Is.EqualTo(new[] { "de" }));
    }

    [Test]
    public void RemoveEntry_RemovesExistingEntry()
    {
        var catalog = CreateCatalog();
        catalog.AddEntry(CreateEntry("menu_start", "Start"));

        catalog.RemoveEntry(EntryKey.Create("menu_start"));

        Assert.That(catalog.Entries, Is.Empty);
    }
}

public sealed class FingerprintTests
{
    [Test]
    public void Create_ProducesDeterministicSha256Fingerprint()
    {
        var first = Fingerprint.Create("canonical-input");
        var second = Fingerprint.Create("canonical-input");

        Assert.That(first.Value, Does.StartWith("sha256:"));
        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void Parse_RejectsInvalidFingerprint()
    {
        Assert.Throws<Errors.DomainValidationException>(() => Fingerprint.Parse("md5:deadbeef"));
    }
}
