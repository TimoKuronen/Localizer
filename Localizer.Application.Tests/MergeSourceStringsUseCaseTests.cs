using Localizer.Application.Import;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Errors;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;

namespace Localizer.Application.Tests;

public sealed class MergeSourceStringsUseCaseTests
{
    private static readonly Locale Spanish = Locale.Create("es");

    [Test]
    public void Merge_AddsNewEntriesWithAllSuppliedFields()
    {
        var catalog = TestCatalogs.CreatePlain();

        var outcome = Merge(catalog, new SourceStringRow
        {
            Key = "settings.title",
            SourceText = "Settings",
            Category = "ui",
            DeveloperNotes = "Window heading.",
            AllowEmptyText = false,
            Context = new Dictionary<string, string> { ["surface"] = "settings" },
            Constraints = new EntryConstraints { MaxGraphemes = 20 },
            ExternalIds = new Dictionary<string, string> { ["app"] = "S1" }
        });

        var entry = catalog.Entries[EntryKey.Create("settings.title")];
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(outcome.AddedCount, Is.EqualTo(1));
            Assert.That(entry.Category, Is.EqualTo("ui"));
            Assert.That(entry.DeveloperNotes, Is.EqualTo("Window heading."));
            Assert.That(entry.Context.Values["surface"], Is.EqualTo("settings"));
            Assert.That(entry.Constraints.MaxGraphemes, Is.EqualTo(20));
            Assert.That(entry.ExternalIds.Values["app"], Is.EqualTo("S1"));
            Assert.That(entry.SyntaxProfileOverride, Is.Null);
        });
    }

    [Test]
    public void Merge_DetectsCompositeProfileFromIndexedPlaceholders()
    {
        var catalog = TestCatalogs.CreatePlain();

        Merge(catalog, new SourceStringRow { Key = "count.label", SourceText = "{0} items" });

        Assert.That(
            catalog.Entries[EntryKey.Create("count.label")].SyntaxProfileOverride,
            Is.EqualTo(MessageSyntaxProfile.Composite));
    }

    [Test]
    public void Merge_ExplicitSyntaxProfileWinsOverDetection()
    {
        var catalog = TestCatalogs.CreatePlain();

        Merge(catalog, new SourceStringRow
        {
            Key = "count.label",
            SourceText = "{0} items",
            SyntaxProfile = MessageSyntaxProfile.Plain
        });

        Assert.That(
            catalog.Entries[EntryKey.Create("count.label")].SyntaxProfileOverride,
            Is.EqualTo(MessageSyntaxProfile.Plain));
    }

    [Test]
    public void Merge_ChangedSourceKeepsApprovedTextAndMarksItStale()
    {
        var catalog = CatalogWithApprovedSpanish("ui.start", "Start", "Empezar");

        var outcome = Merge(catalog, new SourceStringRow { Key = "ui.start", SourceText = "Begin" });

        var entry = catalog.Entries[EntryKey.Create("ui.start")];
        Assert.Multiple(() =>
        {
            Assert.That(outcome.UpdatedCount, Is.EqualTo(1));
            Assert.That(entry.Translations[Spanish].Text, Is.EqualTo("Empezar"));
            Assert.That(entry.Translations[Spanish].State, Is.EqualTo(TranslationState.Approved));
            Assert.That(catalog.GetEffectiveStatus(entry, Spanish), Is.EqualTo(TranslationEffectiveStatus.Stale));
        });
    }

    [Test]
    public void Merge_ChangedDeveloperNotesMarkTranslationsStale()
    {
        var catalog = CatalogWithApprovedSpanish("ui.start", "Start", "Empezar");

        Merge(catalog, new SourceStringRow
        {
            Key = "ui.start",
            SourceText = "Start",
            DeveloperNotes = "Verb, not a noun."
        });

        var entry = catalog.Entries[EntryKey.Create("ui.start")];
        Assert.That(catalog.GetEffectiveStatus(entry, Spanish), Is.EqualTo(TranslationEffectiveStatus.Stale));
    }

    [Test]
    public void Merge_IdenticalRowIsUnchangedAndKeepsApprovedStatus()
    {
        var catalog = CatalogWithApprovedSpanish("ui.start", "Start", "Empezar");

        var outcome = Merge(catalog, new SourceStringRow { Key = "ui.start", SourceText = "Start" });

        var entry = catalog.Entries[EntryKey.Create("ui.start")];
        Assert.Multiple(() =>
        {
            Assert.That(outcome.UnchangedCount, Is.EqualTo(1));
            Assert.That(outcome.UpdatedCount, Is.EqualTo(0));
            Assert.That(catalog.GetEffectiveStatus(entry, Spanish), Is.EqualTo(TranslationEffectiveStatus.Approved));
        });
    }

    [Test]
    public void Merge_AbsentOptionalFieldsLeaveExistingValuesUnchanged()
    {
        var catalog = TestCatalogs.CreatePlain();
        Merge(catalog, new SourceStringRow
        {
            Key = "ui.start",
            SourceText = "Start",
            Category = "ui",
            DeveloperNotes = "Keep short.",
            Context = new Dictionary<string, string> { ["surface"] = "menu" },
            Constraints = new EntryConstraints { MaxLines = 1 },
            ExternalIds = new Dictionary<string, string> { ["app"] = "A" }
        });

        var outcome = Merge(catalog, new SourceStringRow { Key = "ui.start", SourceText = "Start" });

        var entry = catalog.Entries[EntryKey.Create("ui.start")];
        Assert.Multiple(() =>
        {
            Assert.That(outcome.UnchangedCount, Is.EqualTo(1));
            Assert.That(entry.Category, Is.EqualTo("ui"));
            Assert.That(entry.DeveloperNotes, Is.EqualTo("Keep short."));
            Assert.That(entry.Context.Values["surface"], Is.EqualTo("menu"));
            Assert.That(entry.Constraints.MaxLines, Is.EqualTo(1));
            Assert.That(entry.ExternalIds.Values["app"], Is.EqualTo("A"));
        });
    }

    [Test]
    public void Merge_ExternalIdsMergePerNamespace()
    {
        var catalog = TestCatalogs.CreatePlain();
        Merge(catalog, new SourceStringRow
        {
            Key = "ui.start",
            SourceText = "Start",
            ExternalIds = new Dictionary<string, string> { ["first"] = "1" }
        });

        var outcome = Merge(catalog, new SourceStringRow
        {
            Key = "ui.start",
            SourceText = "Start",
            ExternalIds = new Dictionary<string, string> { ["second"] = "2" }
        });

        var ids = catalog.Entries[EntryKey.Create("ui.start")].ExternalIds.Values;
        Assert.Multiple(() =>
        {
            Assert.That(outcome.UpdatedCount, Is.EqualTo(1));
            Assert.That(ids["first"], Is.EqualTo("1"));
            Assert.That(ids["second"], Is.EqualTo("2"));
        });
    }

    [Test]
    public void Merge_DoesNotRemoveEntriesMissingFromTheFile()
    {
        var catalog = TestCatalogs.CreatePlain();
        Merge(catalog, new SourceStringRow { Key = "ui.start", SourceText = "Start" });

        Merge(catalog, new SourceStringRow { Key = "ui.quit", SourceText = "Quit" });

        Assert.That(catalog.Entries.Keys.Select(key => key.Value), Is.EquivalentTo(new[] { "ui.start", "ui.quit" }));
    }

    [Test]
    public void Merge_RestoresTheCatalogWhenALaterRowFailsValidation()
    {
        var catalog = TestCatalogs.CreatePlain();
        Merge(catalog, new SourceStringRow { Key = "ui.start", SourceText = "Start" });

        Assert.Throws<DomainValidationException>(() => Merge(
            catalog,
            new SourceStringRow { Key = "ui.start", SourceText = "Begin" },
            new SourceStringRow { Key = "ui.quit", SourceText = "Quit" },
            new SourceStringRow { Key = "ui.zzz", SourceText = string.Empty }));

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Entries.Keys.Select(key => key.Value), Is.EqualTo(new[] { "ui.start" }));
            Assert.That(catalog.Entries[EntryKey.Create("ui.start")].SourceText, Is.EqualTo("Start"));
        });
    }

    [Test]
    public void Merge_RejectsSourceLocaleMismatch()
    {
        var catalog = TestCatalogs.CreatePlain();

        var outcome = new MergeSourceStringsUseCase().Execute(catalog, new SourceStringsDocument
        {
            SourceLocale = "fi",
            Rows = [new SourceStringRow { Key = "ui.start", SourceText = "Aloita" }]
        });

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.ErrorCode, Is.EqualTo(UseCaseErrorCodes.ImportLocaleMismatch));
            Assert.That(catalog.Entries, Is.Empty);
        });
    }

    private static SourceStringsImportOutcome Merge(Catalog catalog, params SourceStringRow[] rows) =>
        new MergeSourceStringsUseCase().Execute(catalog, new SourceStringsDocument
        {
            SourceLocale = catalog.SourceLocale.Value,
            Rows = rows
        });

    private static Catalog CatalogWithApprovedSpanish(string key, string source, string translation)
    {
        var catalog = TestCatalogs.CreatePlain();
        Merge(catalog, new SourceStringRow { Key = key, SourceText = source });

        var entry = catalog.Entries[EntryKey.Create(key)];
        catalog.ReplaceEntry(entry with
        {
            Translations = new Dictionary<Locale, Translation>
            {
                [Spanish] = new Translation
                {
                    Text = translation,
                    State = TranslationState.Approved,
                    BasedOnFingerprint = catalog.GetCurrentFingerprint(entry),
                    Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
                }
            }
        });

        return catalog;
    }
}
