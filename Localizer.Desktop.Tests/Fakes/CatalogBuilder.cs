using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Syntax;

namespace Localizer.Desktop.Tests.Fakes;

internal static class CatalogBuilder
{
    public static Catalog Create(
        string[] locales,
        MessageSyntaxProfile syntax = MessageSyntaxProfile.Plain,
        string catalogId = "catalog") =>
        new CreateCatalogUseCase().Execute(new CreateCatalogRequest
        {
            CatalogId = catalogId,
            SourceLocale = "en",
            RequiredLocales = locales,
            DefaultSyntaxProfile = syntax
        }).Value!;

    public static Catalog WithEntries(string[] locales, params string[] keys)
    {
        var catalog = Create(locales);
        foreach (var key in keys)
        {
            AddEntry(catalog, key, $"Source {key}");
        }

        return catalog;
    }

    public static void AddEntry(Catalog catalog, string key, string source)
    {
        var added = new AddCatalogEntryUseCase().Execute(
            catalog,
            new AddCatalogEntryRequest { Key = key, SourceText = source });
        Assert.That(added.Succeeded, Is.True, added.ErrorMessage);
    }

    public static void AddDraft(Catalog catalog, string key, string locale, string text)
    {
        var drafted = new SetTranslationDraftUseCase().Execute(
            catalog,
            new SetTranslationDraftRequest { Key = key, Locale = locale, Text = text });
        Assert.That(drafted.Succeeded, Is.True, drafted.ErrorMessage);
    }
}
