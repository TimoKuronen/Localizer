using Localizer.Application.Persistence;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;

namespace Localizer.Application.Tests;

public sealed class CatalogLifecycleUseCaseTests
{
    [Test]
    public void CreateCatalog_SucceedsForValidRequest()
    {
        var result = new CreateCatalogUseCase().Execute(new CreateCatalogRequest
        {
            CatalogId = "pocketmatch",
            SourceLocale = "en",
            RequiredLocales = ["es"],
            DefaultSyntaxProfile = MessageSyntaxProfile.Plain
        });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.CatalogId.Value, Is.EqualTo("pocketmatch"));
        Assert.That(result.Value.SourceLocale.Value, Is.EqualTo("en"));
        Assert.That(result.Value.RequiredLocales.Select(locale => locale.Value), Is.EqualTo(new[] { "es" }));
    }

    [Test]
    public void CreateCatalog_FailsWhenSourceLocaleIsAlsoRequired()
    {
        var result = new CreateCatalogUseCase().Execute(new CreateCatalogRequest
        {
            CatalogId = "pocketmatch",
            SourceLocale = "en",
            RequiredLocales = ["en"]
        });

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("catalog.required_locales.includes_source"));
    }

    [Test]
    public async Task OpenAndSaveCatalog_RoundTripThroughStore()
    {
        var store = new InMemoryCatalogStore();
        var catalog = CreateCatalog();
        var save = await new SaveCatalogUseCase(store).ExecuteAsync("mem://pocketmatch.json", catalog);
        var open = await new OpenCatalogUseCase(store).ExecuteAsync("mem://pocketmatch.json");

        Assert.That(save.Succeeded, Is.True);
        Assert.That(open.Succeeded, Is.True);
        Assert.That(open.Value, Is.SameAs(catalog));
    }

    [Test]
    public async Task OpenCatalog_FailsWhenStoreReportsMissingFile()
    {
        var result = await new OpenCatalogUseCase(new InMemoryCatalogStore())
            .ExecuteAsync("mem://missing.json");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(CatalogPersistenceErrorCodes.IoFailed));
    }

    [Test]
    public void ConfigureCatalogSettings_UpdatesLocalesAndSyntax()
    {
        var catalog = CreateCatalog();
        var result = new ConfigureCatalogSettingsUseCase().Execute(
            catalog,
            new ConfigureCatalogSettingsRequest
            {
                SourceLocale = "en",
                RequiredLocales = ["fi", "es"],
                DefaultSyntaxProfile = MessageSyntaxProfile.Composite
            });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(catalog.RequiredLocales.Select(locale => locale.Value), Is.EqualTo(new[] { "fi", "es" }));
        Assert.That(catalog.DefaultSyntaxProfile, Is.EqualTo(MessageSyntaxProfile.Composite));
    }

    private static Catalog CreateCatalog() =>
        new(
            schemaVersion: 1,
            catalogId: CatalogId.Create("pocketmatch"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("es")],
            defaultSyntaxProfile: MessageSyntaxProfile.Plain);
}

public sealed class CatalogEntryUseCaseTests
{
    [Test]
    public void AddUpdateAndRemoveEntry_Succeed()
    {
        var catalog = CreateCatalog();
        var add = new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.win.confirm_quit",
            SourceText = "Are you sure?",
            DeveloperNotes = "Quit confirm dialog"
        });

        var update = new UpdateCatalogEntryUseCase().Execute(catalog, new UpdateCatalogEntryRequest
        {
            Key = "ui.win.confirm_quit",
            SourceText = "Quit the game?",
            DeveloperNotes = "Updated note"
        });

        Assert.That(add.Succeeded, Is.True);
        Assert.That(update.Succeeded, Is.True);
        Assert.That(catalog.Entries[EntryKey.Create("ui.win.confirm_quit")].SourceText, Is.EqualTo("Quit the game?"));

        var remove = new RemoveCatalogEntryUseCase().Execute(catalog, "ui.win.confirm_quit");

        Assert.That(remove.Succeeded, Is.True);
        Assert.That(catalog.Entries, Is.Empty);
    }

    [Test]
    public void AddEntry_FailsForDuplicateKey()
    {
        var catalog = CreateCatalog();
        var request = new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        };

        Assert.That(new AddCatalogEntryUseCase().Execute(catalog, request).Succeeded, Is.True);
        var duplicate = new AddCatalogEntryUseCase().Execute(catalog, request);

        Assert.That(duplicate.Succeeded, Is.False);
        Assert.That(duplicate.ErrorCode, Is.EqualTo(EntryKeyRules.DuplicateKeyCode));
    }

    [Test]
    public void UpdateEntry_PreservesApprovedTranslationAndMarksStale()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        });

        var entry = catalog.Entries[EntryKey.Create("ui.start")];
        var fingerprint = catalog.GetCurrentFingerprint(entry);
        catalog.ReplaceEntry(entry with
        {
            Translations = new Dictionary<Locale, Translation>
            {
                [Locale.Create("es")] = new Translation
                {
                    Text = "Empezar",
                    State = TranslationState.Approved,
                    BasedOnFingerprint = fingerprint,
                    Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
                }
            }
        });

        var update = new UpdateCatalogEntryUseCase().Execute(catalog, new UpdateCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Begin"
        });

        var updated = catalog.Entries[EntryKey.Create("ui.start")];
        Assert.That(update.Succeeded, Is.True);
        Assert.That(updated.Translations[Locale.Create("es")].Text, Is.EqualTo("Empezar"));
        Assert.That(updated.Translations[Locale.Create("es")].State, Is.EqualTo(TranslationState.Approved));
        Assert.That(catalog.GetEffectiveStatus(updated, Locale.Create("es")), Is.EqualTo(TranslationEffectiveStatus.Stale));
    }

    [Test]
    public void SetTranslationDraft_StoresHumanDraftAgainstCurrentFingerprint()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        });

        var result = new SetTranslationDraftUseCase().Execute(catalog, new SetTranslationDraftRequest
        {
            Key = "ui.start",
            Locale = "es",
            Text = "Empezar"
        });

        var entry = catalog.Entries[EntryKey.Create("ui.start")];
        var translation = entry.Translations[Locale.Create("es")];

        Assert.That(result.Succeeded, Is.True);
        Assert.That(translation.State, Is.EqualTo(TranslationState.Draft));
        Assert.That(translation.Provenance.Origin, Is.EqualTo(TranslationOrigin.Human));
        Assert.That(translation.BasedOnFingerprint, Is.EqualTo(catalog.GetCurrentFingerprint(entry)));
        Assert.That(catalog.GetEffectiveStatus(entry, Locale.Create("es")), Is.EqualTo(TranslationEffectiveStatus.Draft));
    }

    [Test]
    public void SetTranslationDraft_FailsForUnknownLocale()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        });

        var result = new SetTranslationDraftUseCase().Execute(catalog, new SetTranslationDraftRequest
        {
            Key = "ui.start",
            Locale = "fi",
            Text = "Aloita"
        });

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(UseCaseErrorCodes.LocaleNotRequired));
    }

    private static Catalog CreateCatalog() =>
        new(
            schemaVersion: 1,
            catalogId: CatalogId.Create("pocketmatch"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("es")],
            defaultSyntaxProfile: MessageSyntaxProfile.Plain);
}

public sealed class CatalogQueryUseCaseTests
{
    [Test]
    public void GetWorkQueue_ReturnsMissingAndStaleItemsInStableOrder()
    {
        var catalog = CreateCatalogWithMixedStatuses();

        var queue = new GetWorkQueueUseCase().Execute(catalog, WorkQueueFilter.Missing | WorkQueueFilter.Stale);

        Assert.That(queue.Select(item => $"{item.Key.Value}:{item.Locale.Value}:{item.Status}"), Is.EqualTo(new[]
        {
            "ui.help:es:Missing",
            "ui.start:es:Stale"
        }));
    }

    [Test]
    public void GetCatalogStatusSummary_CountsEachEffectiveStatus()
    {
        var catalog = CreateCatalogWithMixedStatuses();

        var summary = new GetCatalogStatusSummaryUseCase().Execute(catalog);

        Assert.That(summary.EntryCount, Is.EqualTo(3));
        Assert.That(summary.MissingCount, Is.EqualTo(1));
        Assert.That(summary.StaleCount, Is.EqualTo(1));
        Assert.That(summary.DraftCount, Is.EqualTo(1));
        Assert.That(summary.ApprovedCount, Is.EqualTo(0));
        Assert.That(summary.RequiredLocaleSlotCount, Is.EqualTo(3));
    }

    [Test]
    public void ValidateCatalog_ReturnsBlockingDiagnosticsForBrokenComposite()
    {
        var catalog = new Catalog(
            schemaVersion: 1,
            catalogId: CatalogId.Create("pocketmatch"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("es")],
            defaultSyntaxProfile: MessageSyntaxProfile.Composite);

        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.score",
            SourceText = "Score {0}"
        });

        new SetTranslationDraftUseCase().Execute(catalog, new SetTranslationDraftRequest
        {
            Key = "ui.score",
            Locale = "es",
            Text = "Puntuacion"
        });

        var result = new ValidateCatalogUseCase().Execute(catalog);

        Assert.That(result.HasBlockingErrors, Is.True);
        Assert.That(result.Diagnostics, Is.Not.Empty);
    }

    private static Catalog CreateCatalogWithMixedStatuses()
    {
        var catalog = new Catalog(
            schemaVersion: 1,
            catalogId: CatalogId.Create("pocketmatch"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("es")],
            defaultSyntaxProfile: MessageSyntaxProfile.Plain);

        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        });
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.help",
            SourceText = "Help"
        });
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.quit",
            SourceText = "Quit"
        });

        var start = catalog.Entries[EntryKey.Create("ui.start")];
        var oldFingerprint = catalog.GetCurrentFingerprint(start);
        catalog.ReplaceEntry(start with
        {
            SourceText = "Begin",
            Translations = new Dictionary<Locale, Translation>
            {
                [Locale.Create("es")] = new Translation
                {
                    Text = "Empezar",
                    State = TranslationState.Approved,
                    BasedOnFingerprint = oldFingerprint,
                    Provenance = new TranslationProvenance { Origin = TranslationOrigin.Human }
                }
            }
        });

        new SetTranslationDraftUseCase().Execute(catalog, new SetTranslationDraftRequest
        {
            Key = "ui.quit",
            Locale = "es",
            Text = "Salir"
        });

        return catalog;
    }
}
