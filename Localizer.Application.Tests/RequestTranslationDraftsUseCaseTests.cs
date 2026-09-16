using Localizer.Application.Drafting;
using Localizer.Application.Time;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;

namespace Localizer.Application.Tests;

public sealed class RequestTranslationDraftsUseCaseTests
{
    [Test]
    public async Task RequestDrafts_AppliesModelDraftsWithProvenance()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start",
            DeveloperNotes = "Short verb for the main menu button."
        });
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.quit",
            SourceText = "Quit"
        });

        var provider = new FakeDraftProvider(
        [
            new TranslationDraftItemResult { Key = "ui.start", TargetLocale = "es", Text = "Empezar" },
            new TranslationDraftItemResult { Key = "ui.quit", TargetLocale = "es", Text = "Salir" }
        ]);
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
        var useCase = new RequestTranslationDraftsUseCase(provider, clock);

        var result = await useCase.ExecuteAsync(catalog, new RequestTranslationDraftsRequest());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.RequestedCount, Is.EqualTo(2));
        Assert.That(result.Value.AppliedCount, Is.EqualTo(2));
        Assert.That(result.Value.RejectionMessages, Is.Empty);

        var start = catalog.Entries[EntryKey.Create("ui.start")].Translations[Locale.Create("es")];
        Assert.That(start.State, Is.EqualTo(TranslationState.Draft));
        Assert.That(start.Text, Is.EqualTo("Empezar"));
        Assert.That(start.Provenance.Origin, Is.EqualTo(TranslationOrigin.Model));
        Assert.That(start.Provenance.ProviderName, Is.EqualTo("fake"));
        Assert.That(start.Provenance.ModelName, Is.EqualTo("test-model"));
        Assert.That(start.Provenance.GeneratedAtUtc, Is.EqualTo(clock.UtcNow));
        Assert.That(catalog.GetEffectiveStatus(catalog.Entries[EntryKey.Create("ui.start")], Locale.Create("es")),
            Is.EqualTo(TranslationEffectiveStatus.Draft));

        Assert.That(provider.LastRequest, Is.Not.Null);
        Assert.That(provider.LastRequest!.Items, Has.Count.EqualTo(2));
        var startRequest = provider.LastRequest.Items.Single(item => item.Key == "ui.start");
        Assert.That(startRequest.DeveloperNotes, Is.EqualTo("Short verb for the main menu button."));
        Assert.That(startRequest.SourceText, Is.EqualTo("Start"));
        Assert.That(startRequest.TargetLocale, Is.EqualTo("es"));
        Assert.That(startRequest.SyntaxProfile, Is.EqualTo("plain"));
    }

    [Test]
    public async Task RequestDrafts_DoesNotAutoApprove()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        });

        var useCase = new RequestTranslationDraftsUseCase(
            new FakeDraftProvider(
            [
                new TranslationDraftItemResult { Key = "ui.start", TargetLocale = "es", Text = "Empezar" }
            ]),
            new FixedClock(DateTimeOffset.UnixEpoch));

        var result = await useCase.ExecuteAsync(catalog, new RequestTranslationDraftsRequest());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(
            catalog.Entries[EntryKey.Create("ui.start")].Translations[Locale.Create("es")].State,
            Is.EqualTo(TranslationState.Draft));
        Assert.That(
            catalog.GetEffectiveStatus(catalog.Entries[EntryKey.Create("ui.start")], Locale.Create("es")),
            Is.Not.EqualTo(TranslationEffectiveStatus.Approved));
    }

    [Test]
    public async Task RequestDrafts_RejectsUnexpectedAndDuplicateResults()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        });

        var useCase = new RequestTranslationDraftsUseCase(
            new FakeDraftProvider(
            [
                new TranslationDraftItemResult { Key = "ui.start", TargetLocale = "es", Text = "Empezar" },
                new TranslationDraftItemResult { Key = "ui.start", TargetLocale = "es", Text = "Iniciar" },
                new TranslationDraftItemResult { Key = "ui.other", TargetLocale = "es", Text = "Otro" }
            ]),
            new FixedClock(DateTimeOffset.UnixEpoch));

        var result = await useCase.ExecuteAsync(catalog, new RequestTranslationDraftsRequest());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.AppliedCount, Is.EqualTo(1));
        Assert.That(result.Value.RejectionMessages, Has.Count.EqualTo(2));
        Assert.That(
            catalog.Entries[EntryKey.Create("ui.start")].Translations[Locale.Create("es")].Text,
            Is.EqualTo("Empezar"));
    }

    [Test]
    public async Task RequestDrafts_ProviderFailureDoesNotCorruptCatalog()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        });

        var useCase = new RequestTranslationDraftsUseCase(
            new ThrowingDraftProvider(),
            new FixedClock(DateTimeOffset.UnixEpoch));

        var result = await useCase.ExecuteAsync(catalog, new RequestTranslationDraftsRequest());

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(UseCaseErrorCodes.DraftProviderFailed));
        Assert.That(catalog.Entries[EntryKey.Create("ui.start")].Translations, Is.Empty);
    }

    [Test]
    public async Task RequestDrafts_OnlyTargetsMissingAndStale()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.missing",
            SourceText = "Missing"
        });
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.draft",
            SourceText = "Draft"
        });
        new SetTranslationDraftUseCase().Execute(catalog, new SetTranslationDraftRequest
        {
            Key = "ui.draft",
            Locale = "es",
            Text = "Borrador"
        });

        var provider = new FakeDraftProvider(
        [
            new TranslationDraftItemResult { Key = "ui.missing", TargetLocale = "es", Text = "Falta" }
        ]);
        var useCase = new RequestTranslationDraftsUseCase(provider, new FixedClock(DateTimeOffset.UnixEpoch));

        var result = await useCase.ExecuteAsync(catalog, new RequestTranslationDraftsRequest());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.RequestedCount, Is.EqualTo(1));
        Assert.That(provider.LastRequest!.Items.Single().Key, Is.EqualTo("ui.missing"));
        Assert.That(
            catalog.Entries[EntryKey.Create("ui.draft")].Translations[Locale.Create("es")].Text,
            Is.EqualTo("Borrador"));
    }

    private static Catalog CreateCatalog()
    {
        var result = new CreateCatalogUseCase().Execute(new CreateCatalogRequest
        {
            CatalogId = "demo",
            SourceLocale = "en",
            RequiredLocales = ["es"],
            DefaultSyntaxProfile = MessageSyntaxProfile.Plain
        });
        Assert.That(result.Succeeded, Is.True);
        return result.Value!;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeDraftProvider(IReadOnlyList<TranslationDraftItemResult> items) : ITranslationDraftProvider
    {
        public TranslationDraftBatchRequest? LastRequest { get; private set; }

        public Task<TranslationDraftBatchResult> DraftAsync(
            TranslationDraftBatchRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new TranslationDraftBatchResult
            {
                ProviderName = "fake",
                ModelName = "test-model",
                Items = items
            });
        }
    }

    private sealed class ThrowingDraftProvider : ITranslationDraftProvider
    {
        public Task<TranslationDraftBatchResult> DraftAsync(
            TranslationDraftBatchRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider offline");
    }
}
