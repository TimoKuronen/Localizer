using Localizer.Application.Export;
using Localizer.Application.Persistence;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;

namespace Localizer.Application.Tests;

public sealed class ExportDraftPreviewUseCaseTests
{
    private static readonly Locale Spanish = Locale.Create("es");

    [Test]
    public async Task Execute_IncludesApprovedDraftAndStaleAndListsMissingAsLeftOut()
    {
        var catalog = CreateCatalog(MessageSyntaxProfile.Plain);
        AddEntry(catalog, "a.approved", "A");
        AddEntry(catalog, "b.draft", "B");
        AddEntry(catalog, "c.stale", "C");
        AddEntry(catalog, "d.missing", "D");
        SetDraft(catalog, "a.approved", "Aa");
        Approve(catalog, "a.approved");
        SetDraft(catalog, "b.draft", "Bb");
        SetDraft(catalog, "c.stale", "Cc");
        Approve(catalog, "c.stale");
        new UpdateCatalogEntryUseCase().Execute(catalog, new UpdateCatalogEntryRequest { Key = "c.stale", SourceText = "C2" });
        var writer = new RecordingWriter();

        var outcome = await new ExportDraftPreviewUseCase(writer).ExecuteAsync(catalog, "out");

        var summary = outcome.Report.LocaleSummaries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(writer.Files.Single().Texts.Select(pair => pair.Key), Is.EqualTo(new[] { "a.approved", "b.draft", "c.stale" }));
            Assert.That(writer.Files.Single().Texts.Select(pair => pair.Value), Is.EqualTo(new[] { "Aa", "Bb", "Cc" }));
            Assert.That(summary.ApprovedCount, Is.EqualTo(1));
            Assert.That(summary.DraftCount, Is.EqualTo(1));
            Assert.That(summary.StaleCount, Is.EqualTo(1));
            Assert.That(summary.MissingCount, Is.EqualTo(1));
            Assert.That(outcome.Report.SkippedItems.Single().Key, Is.EqualTo("d.missing"));
            Assert.That(outcome.Report.SkippedItems.Single().Reason, Is.EqualTo(DraftPreviewSkippedItem.SkipReason.Missing));
        });
    }

    [Test]
    public async Task Execute_LeavesOutTranslationsWithBlockingValidationErrors()
    {
        var catalog = CreateCatalog(MessageSyntaxProfile.Composite);
        AddEntry(catalog, "count.label", "{0} items");
        AddEntry(catalog, "ui.quit", "Quit");
        SetDraft(catalog, "count.label", "elementos");
        SetDraft(catalog, "ui.quit", "Salir");
        var writer = new RecordingWriter();

        var outcome = await new ExportDraftPreviewUseCase(writer).ExecuteAsync(catalog, "out");

        var skipped = outcome.Report.SkippedItems.Single();
        Assert.Multiple(() =>
        {
            Assert.That(writer.Files.Single().Texts.Select(pair => pair.Key), Is.EqualTo(new[] { "ui.quit" }));
            Assert.That(skipped.Key, Is.EqualTo("count.label"));
            Assert.That(skipped.Reason, Is.EqualTo(DraftPreviewSkippedItem.SkipReason.InvalidTranslation));
            Assert.That(outcome.Report.LocaleSummaries.Single().InvalidCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Execute_DoesNotChangeTheCatalog()
    {
        var catalog = CreateCatalog(MessageSyntaxProfile.Plain);
        AddEntry(catalog, "ui.start", "Start");
        SetDraft(catalog, "ui.start", "Empezar");

        await new ExportDraftPreviewUseCase(new RecordingWriter()).ExecuteAsync(catalog, "out");

        var entry = catalog.Entries[EntryKey.Create("ui.start")];
        Assert.Multiple(() =>
        {
            Assert.That(entry.Translations[Spanish].State, Is.EqualTo(TranslationState.Draft));
            Assert.That(catalog.GetEffectiveStatus(entry, Spanish), Is.EqualTo(TranslationEffectiveStatus.Draft));
        });
    }

    [Test]
    public async Task Execute_ReportsWriterFailureAsOutcome()
    {
        var catalog = CreateCatalog(MessageSyntaxProfile.Plain);
        var writer = new RecordingWriter
        {
            Failure = new CatalogPersistenceException(CatalogPersistenceErrorCodes.IoFailed, "disk full")
        };

        var outcome = await new ExportDraftPreviewUseCase(writer).ExecuteAsync(catalog, "out");

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.ErrorCode, Is.EqualTo(CatalogPersistenceErrorCodes.IoFailed));
            Assert.That(outcome.WrittenFiles, Is.Empty);
        });
    }

    private static Catalog CreateCatalog(MessageSyntaxProfile profile) =>
        new(
            schemaVersion: 1,
            catalogId: CatalogId.Create("sample-app"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Spanish],
            defaultSyntaxProfile: profile);

    private static void AddEntry(Catalog catalog, string key, string source)
    {
        var added = new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest { Key = key, SourceText = source });
        Assert.That(added.Succeeded, Is.True, added.ErrorMessage);
    }

    private static void SetDraft(Catalog catalog, string key, string text)
    {
        var drafted = new SetTranslationDraftUseCase().Execute(
            catalog,
            new SetTranslationDraftRequest { Key = key, Locale = "es", Text = text });
        Assert.That(drafted.Succeeded, Is.True, drafted.ErrorMessage);
    }

    private static void Approve(Catalog catalog, string key)
    {
        var approved = new ApproveTranslationUseCase().Execute(catalog, new ApproveTranslationRequest { Key = key, Locale = "es" });
        Assert.That(approved.Succeeded, Is.True, approved.ErrorMessage);
    }

    private sealed class RecordingWriter : IDraftPreviewWriter
    {
        public IReadOnlyList<LocaleTexts> Files { get; private set; } = [];

        public Exception? Failure { get; init; }

        public Task<IReadOnlyList<string>> WriteAsync(
            string outputDirectory,
            IReadOnlyList<LocaleTexts> files,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Files = files;
            return Task.FromResult<IReadOnlyList<string>>(
                files.Select(file => $"{outputDirectory}/{file.Locale.Value}.json").ToList());
        }
    }
}
