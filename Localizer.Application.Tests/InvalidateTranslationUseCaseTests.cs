using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;

namespace Localizer.Application.Tests;

public sealed class InvalidateTranslationUseCaseTests
{
    [Test]
    public void Invalidate_MarksApprovedTranslationStaleAndPreservesText()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        });
        new SetTranslationDraftUseCase().Execute(catalog, new SetTranslationDraftRequest
        {
            Key = "ui.start",
            Locale = "es",
            Text = "Empezar"
        });
        new ApproveTranslationUseCase().Execute(catalog, new ApproveTranslationRequest
        {
            Key = "ui.start",
            Locale = "es"
        });

        var result = new InvalidateTranslationUseCase().Execute(catalog, new InvalidateTranslationRequest
        {
            Key = "ui.start",
            Locale = "es"
        });

        var entry = catalog.Entries[EntryKey.Create("ui.start")];
        var translation = entry.Translations[Locale.Create("es")];

        Assert.That(result.Succeeded, Is.True);
        Assert.That(translation.Text, Is.EqualTo("Empezar"));
        Assert.That(translation.State, Is.EqualTo(TranslationState.Approved));
        Assert.That(catalog.GetEffectiveStatus(entry, Locale.Create("es")), Is.EqualTo(TranslationEffectiveStatus.Stale));
    }

    [Test]
    public void Invalidate_FailsWhenMissing()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        });

        var result = new InvalidateTranslationUseCase().Execute(catalog, new InvalidateTranslationRequest
        {
            Key = "ui.start",
            Locale = "es"
        });

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(UseCaseErrorCodes.TranslationNotFound));
    }

    [Test]
    public void Invalidate_IsNoOpWhenAlreadyStale()
    {
        var catalog = CreateCatalog();
        new AddCatalogEntryUseCase().Execute(catalog, new AddCatalogEntryRequest
        {
            Key = "ui.start",
            SourceText = "Start"
        });
        new SetTranslationDraftUseCase().Execute(catalog, new SetTranslationDraftRequest
        {
            Key = "ui.start",
            Locale = "es",
            Text = "Empezar"
        });
        new InvalidateTranslationUseCase().Execute(catalog, new InvalidateTranslationRequest
        {
            Key = "ui.start",
            Locale = "es"
        });

        var fingerprintBefore = catalog.Entries[EntryKey.Create("ui.start")]
            .Translations[Locale.Create("es")].BasedOnFingerprint;

        var result = new InvalidateTranslationUseCase().Execute(catalog, new InvalidateTranslationRequest
        {
            Key = "ui.start",
            Locale = "es"
        });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(
            catalog.Entries[EntryKey.Create("ui.start")].Translations[Locale.Create("es")].BasedOnFingerprint,
            Is.EqualTo(fingerprintBefore));
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
}
