using Localizer.Core.Catalogs;
using Localizer.Core.Fingerprints;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;
using Localizer.Core.Validation;

namespace Localizer.Core.Tests;

public sealed class CatalogValidatorTests
{
    private static Catalog CreateCatalog() =>
        new(
            schemaVersion: 1,
            catalogId: CatalogId.Create("runner-game"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("fi"), Locale.Create("es")],
            defaultSyntaxProfile: MessageSyntaxProfile.Composite);

    private static Translation CreateTranslation(Catalog catalog, CatalogEntry entry, string text)
    {
        return new Translation
        {
            Text = text,
            State = TranslationState.Draft,
            BasedOnFingerprint = catalog.GetCurrentFingerprint(entry),
            Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
        };
    }

    [Test]
    public void Validate_ReturnsEmptyResultForValidCompositeEntry()
    {
        var catalog = CreateCatalog();
        var entry = new CatalogEntry
        {
            Key = EntryKey.Create("score_label"),
            SourceText = "Score: {0}",
            Translations = new Dictionary<Locale, Translation>
            {
                [Locale.Create("fi")] = CreateTranslation(catalog, new CatalogEntry
                {
                    Key = EntryKey.Create("score_label"),
                    SourceText = "Score: {0}"
                }, "Pisteet: {0}")
            }
        };
        catalog.AddEntry(entry);

        var result = CatalogValidator.Validate(catalog);

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.HasBlockingErrors, Is.False);
    }

    [Test]
    public void Validate_ReturnsMultipleDiagnosticsInStableOrder()
    {
        var catalog = CreateCatalog();
        var fi = Locale.Create("fi");
        var es = Locale.Create("es");
        var entry = new CatalogEntry
        {
            Key = EntryKey.Create("score_label"),
            SourceText = "Score: {0} and {1}",
            Constraints = new EntryConstraints { MaxGraphemes = 3 },
            Translations = new Dictionary<Locale, Translation>
            {
                [fi] = CreateTranslation(catalog, new CatalogEntry
                {
                    Key = EntryKey.Create("score_label"),
                    SourceText = "Score: {0} and {1}"
                }, "Pisteet: {1}"),
                [es] = CreateTranslation(catalog, new CatalogEntry
                {
                    Key = EntryKey.Create("score_label"),
                    SourceText = "Score: {0} and {1}"
                }, "Puntos: {0} and {1} and {2}")
            }
        };
        catalog.AddEntry(entry);

        var firstRun = CatalogValidator.Validate(catalog);
        var secondRun = CatalogValidator.Validate(catalog);

        Assert.That(firstRun.Diagnostics, Is.Not.Empty);
        Assert.That(firstRun.Diagnostics.Select(diagnostic => diagnostic.Code).ToList(),
            Is.EqualTo(secondRun.Diagnostics.Select(diagnostic => diagnostic.Code).ToList()));
        Assert.That(firstRun.Diagnostics.Select(diagnostic => diagnostic.Locale?.Value).ToList(),
            Is.EqualTo(secondRun.Diagnostics.Select(diagnostic => diagnostic.Locale?.Value).ToList()));
    }

    [Test]
    public void Validate_DoesNotMutateCatalogContent()
    {
        var catalog = CreateCatalog();
        var entry = new CatalogEntry
        {
            Key = EntryKey.Create("score_label"),
            SourceText = "Score: {0}",
            Translations = new Dictionary<Locale, Translation>
            {
                [Locale.Create("fi")] = CreateTranslation(catalog, new CatalogEntry
                {
                    Key = EntryKey.Create("score_label"),
                    SourceText = "Score: {0}"
                }, "Pisteet: {1}")
            }
        };
        catalog.AddEntry(entry);
        var snapshot = catalog.Entries[entry.Key].Translations[Locale.Create("fi")].Text;

        _ = CatalogValidator.Validate(catalog);

        Assert.That(catalog.Entries[entry.Key].Translations[Locale.Create("fi")].Text, Is.EqualTo(snapshot));
    }
}

public sealed class ValidationPolicyTests
{
    private static Catalog CreateCatalog() =>
        new(
            schemaVersion: 1,
            catalogId: CatalogId.Create("runner-game"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("fi")],
            defaultSyntaxProfile: MessageSyntaxProfile.Composite);

    private static CatalogEntry CreateEntry(
        Catalog catalog,
        string key,
        string sourceText,
        string? translationText,
        TranslationState translationState = TranslationState.Approved)
    {
        var entryKey = EntryKey.Create(key);
        var entryWithoutTranslation = new CatalogEntry
        {
            Key = entryKey,
            SourceText = sourceText
        };

        Dictionary<Locale, Translation> translations = new();
        if (translationText is not null)
        {
            translations[Locale.Create("fi")] = new Translation
            {
                Text = translationText,
                State = translationState,
                BasedOnFingerprint = catalog.GetCurrentFingerprint(entryWithoutTranslation),
                Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
            };
        }

        return entryWithoutTranslation with { Translations = translations };
    }

    [Test]
    public void ApprovalValidationPolicy_BlocksApprovalWhenPlaceholderStructureIsInvalid()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry(catalog, "score_label", "Score: {0}", "Pisteet: {1}");
        catalog.AddEntry(entry);

        var result = ApprovalValidationPolicy.Evaluate(catalog, entry, Locale.Create("fi"));

        Assert.That(result.HasBlockingErrors, Is.True);
        Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == DiagnosticCodes.SyntaxPlaceholderIndexMismatch));
    }

    [Test]
    public void ExportValidationPolicy_BlocksMissingTranslation()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry(catalog, "score_label", "Score: {0}", translationText: null);
        catalog.AddEntry(entry);

        var result = ExportValidationPolicy.Evaluate(catalog);

        Assert.That(result.HasBlockingErrors, Is.True);
        Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == DiagnosticCodes.ExportTranslationMissing));
    }

    [Test]
    public void ExportValidationPolicy_BlocksDraftTranslation()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry(catalog, "score_label", "Score: {0}", "Pisteet: {0}", TranslationState.Draft);
        catalog.AddEntry(entry);

        var result = ExportValidationPolicy.Evaluate(catalog);

        Assert.That(result.HasBlockingErrors, Is.True);
        Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == DiagnosticCodes.ExportTranslationDraft));
    }

    [Test]
    public void ExportValidationPolicy_BlocksStaleTranslation()
    {
        var catalog = CreateCatalog();
        var entryKey = EntryKey.Create("score_label");
        var skeleton = new CatalogEntry
        {
            Key = entryKey,
            SourceText = "Score: {0}"
        };
        var staleFingerprint = Fingerprint.Parse(
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var entry = skeleton with
        {
            Translations = new Dictionary<Locale, Translation>
            {
                [Locale.Create("fi")] = new Translation
                {
                    Text = "Pisteet: {0}",
                    State = TranslationState.Approved,
                    BasedOnFingerprint = staleFingerprint,
                    Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
                }
            }
        };
        catalog.AddEntry(entry);

        var result = ExportValidationPolicy.Evaluate(catalog);

        Assert.That(result.HasBlockingErrors, Is.True);
        Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == DiagnosticCodes.ExportTranslationStale));
    }

    [Test]
    public void ExportValidationPolicy_BlocksInvalidApprovedTranslation()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry(catalog, "score_label", "Score: {0}", "Pisteet: {1}");
        catalog.AddEntry(entry);

        var result = ExportValidationPolicy.Evaluate(catalog);

        Assert.That(result.HasBlockingErrors, Is.True);
        Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Code == DiagnosticCodes.ExportTranslationInvalid));
    }

    [Test]
    public void ExportValidationPolicy_AllowsFullyApprovedValidCatalog()
    {
        var catalog = CreateCatalog();
        var entry = CreateEntry(catalog, "score_label", "Score: {0}", "Pisteet: {0}");
        catalog.AddEntry(entry);

        var result = ExportValidationPolicy.Evaluate(catalog);

        Assert.That(result.HasBlockingErrors, Is.False);
    }
}
