using Localizer.Application.Import;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;

namespace Localizer.Application.Tests;

public sealed class FindEntriesNotInSourceUseCaseTests
{
    [Test]
    public async Task ListsOnlyKeysTheSourceOmits_ComparingOrdinally()
    {
        var catalog = CatalogWith("kept", "Renamed", "gone");

        var result = await Execute(catalog, "en", "kept", "renamed", "added");

        Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
        Assert.That(result.Value!.Select(entry => entry.Key), Is.EqualTo(new[] { "Renamed", "gone" }));
        Assert.That(catalog.Entries, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task MarksEntriesWithApprovedText()
    {
        var catalog = CatalogWith("approved", "drafted", "kept");
        Draft(catalog, "approved");
        Draft(catalog, "drafted");
        Assert.That(
            new ApproveTranslationUseCase()
                .Execute(catalog, new ApproveTranslationRequest { Key = "approved", Locale = "es" })
                .Succeeded,
            Is.True);

        var result = await Execute(catalog, "en", "kept");

        Assert.That(
            result.Value!.Select(entry => (entry.Key, entry.HasApprovedText)),
            Is.EqualTo(new[] { ("approved", true), ("drafted", false) }));
    }

    [Test]
    public async Task RejectsASourceWithoutKeys()
    {
        var result = await Execute(CatalogWith("a"), "en");

        Assert.That(result.ErrorCode, Is.EqualTo(UseCaseErrorCodes.ImportSourceEmpty));
    }

    [Test]
    public async Task RejectsAnotherSourceLocale()
    {
        var result = await Execute(CatalogWith("a"), "de", "a");

        Assert.That(result.ErrorCode, Is.EqualTo(UseCaseErrorCodes.ImportLocaleMismatch));
    }

    private static Task<UseCaseResult<IReadOnlyList<SourceMissingEntry>>> Execute(
        Catalog catalog,
        string sourceLocale,
        params string[] keys)
    {
        var reader = new FixedSourceReader(new SourceStringsDocument
        {
            SourceLocale = sourceLocale,
            Rows = keys.Select(key => new SourceStringRow { Key = key, SourceText = key }).ToList()
        });
        return new FindEntriesNotInSourceUseCase(reader, new UnusedCsvReader())
            .FromSourceStringsAsync(catalog, "source.json");
    }

    private static Catalog CatalogWith(params string[] keys)
    {
        var catalog = new CreateCatalogUseCase().Execute(new CreateCatalogRequest
        {
            CatalogId = "catalog",
            SourceLocale = "en",
            RequiredLocales = ["es"]
        }).Value!;
        foreach (var key in keys)
        {
            var added = new AddCatalogEntryUseCase().Execute(
                catalog,
                new AddCatalogEntryRequest { Key = key, SourceText = $"Source {key}" });
            Assert.That(added.Succeeded, Is.True, added.ErrorMessage);
        }

        return catalog;
    }

    private static void Draft(Catalog catalog, string key)
    {
        var drafted = new SetTranslationDraftUseCase().Execute(
            catalog,
            new SetTranslationDraftRequest { Key = key, Locale = "es", Text = "Texto" });
        Assert.That(drafted.Succeeded, Is.True, drafted.ErrorMessage);
    }

    private sealed class FixedSourceReader(SourceStringsDocument document) : ISourceStringsReader
    {
        public Task<SourceStringsDocument> ReadAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(document);
    }

    private sealed class UnusedCsvReader : IStringTableCsvReader
    {
        public Task<StringTableCsvDocument> ReadAsync(
            string path,
            string sourceLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
