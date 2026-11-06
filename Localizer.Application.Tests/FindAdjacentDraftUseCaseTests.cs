using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;

namespace Localizer.Application.Tests;

public sealed class FindAdjacentDraftUseCaseTests
{
    private readonly FindAdjacentDraftUseCase _useCase = new(new GetWorkQueueUseCase());

    [Test]
    public void FindNext_ReturnsFirstDraftWithoutPosition()
    {
        var catalog = CreateCatalog(("a", "es"), ("b", "fr"));

        Assert.That(Describe(_useCase.FindNext(catalog, null, null)), Is.EqualTo("a/es"));
    }

    [Test]
    public void FindNext_StepsThroughLocalesBeforeTheNextEntry()
    {
        var catalog = CreateCatalog(("a", "es"), ("a", "fr"), ("b", "es"));

        Assert.That(Describe(_useCase.FindNext(catalog, "a", "es")), Is.EqualTo("a/fr"));
        Assert.That(Describe(_useCase.FindNext(catalog, "a", "fr")), Is.EqualTo("b/es"));
    }

    [Test]
    public void FindNext_SkipsApprovedItems()
    {
        var catalog = CreateCatalog(("a", "es"), ("c", "es"));
        DraftAndApprove(catalog, "b", "es");

        Assert.That(Describe(_useCase.FindNext(catalog, "a", "es")), Is.EqualTo("c/es"));
    }

    [Test]
    public void FindNext_ContinuesFromAnItemThatIsNoLongerDraft()
    {
        var catalog = CreateCatalog(("c", "es"));
        DraftAndApprove(catalog, "b", "es");

        Assert.That(Describe(_useCase.FindNext(catalog, "b", "es")), Is.EqualTo("c/es"));
    }

    [Test]
    public void FindNext_WrapsToFirstDraft()
    {
        var catalog = CreateCatalog(("a", "es"), ("b", "es"));

        Assert.That(Describe(_useCase.FindNext(catalog, "b", "es")), Is.EqualTo("a/es"));
    }

    [Test]
    public void FindPrevious_StepsBackThroughLocales()
    {
        var catalog = CreateCatalog(("a", "es"), ("a", "fr"), ("b", "es"));

        Assert.That(Describe(_useCase.FindPrevious(catalog, "b", "es")), Is.EqualTo("a/fr"));
        Assert.That(Describe(_useCase.FindPrevious(catalog, "a", "fr")), Is.EqualTo("a/es"));
    }

    [Test]
    public void FindPrevious_WrapsToLastDraft()
    {
        var catalog = CreateCatalog(("a", "es"), ("b", "es"));

        Assert.That(Describe(_useCase.FindPrevious(catalog, "a", "es")), Is.EqualTo("b/es"));
    }

    [Test]
    public void ReturnsNullWhenNoDraftsRemain()
    {
        var catalog = CreateCatalog();
        DraftAndApprove(catalog, "a", "es");

        Assert.Multiple(() =>
        {
            Assert.That(_useCase.FindNext(catalog, "a", "es"), Is.Null);
            Assert.That(_useCase.FindPrevious(catalog, "a", "es"), Is.Null);
        });
    }

    private static string? Describe(WorkQueueItem? item) =>
        item is null ? null : $"{item.Key.Value}/{item.Locale.Value}";

    private static Catalog CreateCatalog(params (string Key, string Locale)[] drafts)
    {
        var catalog = new CreateCatalogUseCase().Execute(new CreateCatalogRequest
        {
            CatalogId = "catalog",
            SourceLocale = "en",
            RequiredLocales = ["es", "fr"]
        }).Value!;

        foreach (var (key, locale) in drafts)
        {
            Draft(catalog, key, locale);
        }

        return catalog;
    }

    private static void DraftAndApprove(Catalog catalog, string key, string locale)
    {
        Draft(catalog, key, locale);
        var result = new ApproveTranslationUseCase().Execute(
            catalog,
            new ApproveTranslationRequest { Key = key, Locale = locale });
        Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
    }

    private static void Draft(Catalog catalog, string key, string locale)
    {
        if (!catalog.Entries.Keys.Any(existing => existing.Value == key))
        {
            var added = new AddCatalogEntryUseCase().Execute(
                catalog,
                new AddCatalogEntryRequest { Key = key, SourceText = $"Source {key}" });
            Assert.That(added.Succeeded, Is.True, added.ErrorMessage);
        }

        var drafted = new SetTranslationDraftUseCase().Execute(
            catalog,
            new SetTranslationDraftRequest { Key = key, Locale = locale, Text = $"Text {key} {locale}" });
        Assert.That(drafted.Succeeded, Is.True, drafted.ErrorMessage);
    }
}
