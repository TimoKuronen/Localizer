using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;

namespace Localizer.Application.Tests;

public sealed class ApproveStaleTranslationUseCaseTests
{
    private readonly ApproveStaleTranslationUseCase _useCase = new();

    [Test]
    public void ApprovesUnchangedTextAgainstTheCurrentSource()
    {
        var catalog = CreateCatalog("Start", "Empezar", "Start game");

        var result = Execute(catalog);

        Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
        Assert.That(Status(catalog), Is.EqualTo(TranslationEffectiveStatus.Approved));
        Assert.That(Stored(catalog).Text, Is.EqualTo("Empezar"));
    }

    [Test]
    public void BlockedValidation_LeavesTheTranslationStale()
    {
        var catalog = CreateCatalog("Coins", "Monedas", "Coins {0}");
        var before = Stored(catalog);

        var result = Execute(catalog);

        Assert.That(result.ErrorCode, Is.EqualTo(UseCaseErrorCodes.ValidationBlocked));
        Assert.That(Stored(catalog), Is.EqualTo(before));
        Assert.That(Status(catalog), Is.EqualTo(TranslationEffectiveStatus.Stale));
    }

    [Test]
    public void RejectsATranslationThatIsNotStale()
    {
        var catalog = CreateCatalog("Start", "Empezar", "Start");

        var result = Execute(catalog);

        Assert.That(result.ErrorCode, Is.EqualTo(UseCaseErrorCodes.TranslationNotStale));
        Assert.That(Status(catalog), Is.EqualTo(TranslationEffectiveStatus.Draft));
    }

    private UseCaseResult Execute(Catalog catalog) =>
        _useCase.Execute(catalog, new ApproveStaleTranslationRequest { Key = "a", Locale = "es" });

    private static Translation Stored(Catalog catalog) =>
        catalog.Entries[EntryKey.Create("a")].Translations[Locale.Create("es")];

    private static TranslationEffectiveStatus Status(Catalog catalog) =>
        catalog.GetEffectiveStatus(catalog.Entries[EntryKey.Create("a")], Locale.Create("es"));

    private static Catalog CreateCatalog(string source, string translation, string laterSource)
    {
        var catalog = new CreateCatalogUseCase().Execute(new CreateCatalogRequest
        {
            CatalogId = "catalog",
            SourceLocale = "en",
            RequiredLocales = ["es"],
            DefaultSyntaxProfile = MessageSyntaxProfile.Composite
        }).Value!;
        var added = new AddCatalogEntryUseCase().Execute(
            catalog,
            new AddCatalogEntryRequest { Key = "a", SourceText = source });
        Assert.That(added.Succeeded, Is.True, added.ErrorMessage);
        var drafted = new SetTranslationDraftUseCase().Execute(
            catalog,
            new SetTranslationDraftRequest { Key = "a", Locale = "es", Text = translation });
        Assert.That(drafted.Succeeded, Is.True, drafted.ErrorMessage);

        var entry = catalog.Entries[EntryKey.Create("a")];
        catalog.ReplaceEntry(entry with { SourceText = laterSource });
        return catalog;
    }
}
