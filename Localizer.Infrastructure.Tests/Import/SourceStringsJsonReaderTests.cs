using Localizer.Application.Import;
using Localizer.Application.Persistence;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;
using Localizer.Infrastructure.Export;
using Localizer.Infrastructure.Import;

namespace Localizer.Infrastructure.Tests.Import;

public sealed class SourceStringsJsonReaderTests
{
    [Test]
    public async Task Read_ParsesSampleFixtureInOrdinalKeyOrder()
    {
        var document = await new SourceStringsJsonReader().ReadAsync(GetFixturePath("source-strings-sample.json"));

        Assert.That(document.SourceLocale, Is.EqualTo("en"));
        Assert.That(document.Rows.Select(row => row.Key), Is.EqualTo(new[]
        {
            "dialog.delete.confirm",
            "items.count.one",
            "items.count.other",
            "settings.title"
        }));

        var dialog = document.Rows[0];
        Assert.Multiple(() =>
        {
            Assert.That(dialog.SourceText, Is.EqualTo("Delete \"{0}\"?"));
            Assert.That(dialog.DeveloperNotes, Is.EqualTo("Confirmation prompt. {0} is the item name."));
            Assert.That(dialog.Context!["surface"], Is.EqualTo("confirmation-dialog"));
            Assert.That(dialog.Constraints!.MaxLines, Is.EqualTo(2));
            Assert.That(dialog.ExternalIds!["app"], Is.EqualTo("dlg-017"));
            Assert.That(document.Rows[2].SyntaxProfile, Is.EqualTo(MessageSyntaxProfile.Composite));
            Assert.That(document.Rows[3].Constraints!.ForbiddenTerms, Is.EqualTo(new[] { "Preferences" }));
        });
    }

    [Test]
    public async Task Read_NormalizesSourceLocale()
    {
        var path = await WriteAsync("""{"schemaVersion":1,"sourceLocale":"PT_br","entries":[]}""");

        var document = await new SourceStringsJsonReader().ReadAsync(path);

        Assert.That(document.SourceLocale, Is.EqualTo("pt-BR"));
    }

    [TestCase("""{"schemaVersion":1,"sourceLocale":"en","entries":[{"key":"a","sourceText":"x"},{"key":"a","sourceText":"y"}]}""", "duplicate key")]
    [TestCase("""{"schemaVersion":1,"sourceLocale":"en","entries":[{"key":"bad key","sourceText":"x"}]}""", "invalid")]
    [TestCase("""{"schemaVersion":1,"sourceLocale":"en","entries":[{"key":"a"}]}""", "requires sourceText")]
    [TestCase("""{"schemaVersion":1,"sourceLocale":"en","entries":[{"key":"a","sourceText":"x","developerNote":"typo"}]}""", "malformed")]
    [TestCase("""{"schemaVersion":1,"sourceLocale":"en","entries":[{"key":"a","sourceText":"x","syntaxProfile":"icu"}]}""", "unsupported syntaxProfile")]
    [TestCase("""{"schemaVersion":1,"sourceLocale":"","entries":[]}""", "sourceLocale")]
    [TestCase("""{"schemaVersion":1,"sourceLocale":"en"}""", "entries array")]
    [TestCase("""{"sourceLocale":"en","entries":[]}""", "requires schemaVersion")]
    [TestCase("not json", "malformed")]
    public async Task Read_RejectsMalformedDocuments(string json, string expectedMessagePart)
    {
        var path = await WriteAsync(json);

        var exception = Assert.ThrowsAsync<CatalogPersistenceException>(
            () => new SourceStringsJsonReader().ReadAsync(path));

        Assert.That(exception!.Code, Is.EqualTo(CatalogPersistenceErrorCodes.Malformed));
        Assert.That(exception.Message, Does.Contain(expectedMessagePart));
    }

    [Test]
    public async Task Read_RejectsUnsupportedSchemaVersion()
    {
        var path = await WriteAsync("""{"schemaVersion":2,"sourceLocale":"en","entries":[]}""");

        var exception = Assert.ThrowsAsync<CatalogPersistenceException>(
            () => new SourceStringsJsonReader().ReadAsync(path));

        Assert.That(exception!.Code, Is.EqualTo(CatalogPersistenceErrorCodes.UnsupportedVersion));
    }

    [Test]
    public async Task Read_RejectsByteOrderMark()
    {
        var path = Path.Combine(TempDirectory.Create("localizer-source-strings-tests"), "bom.json");
        await File.WriteAllBytesAsync(
            path,
            [0xEF, 0xBB, 0xBF, .. "{\"schemaVersion\":1,\"sourceLocale\":\"en\",\"entries\":[]}"u8.ToArray()]);

        var exception = Assert.ThrowsAsync<CatalogPersistenceException>(
            () => new SourceStringsJsonReader().ReadAsync(path));

        Assert.That(exception!.Code, Is.EqualTo(CatalogPersistenceErrorCodes.ByteOrderMarkPresent));
    }

    [Test]
    public async Task Import_ThenRepeat_IsUnchangedAndExportsApprovedRuntimeJson()
    {
        var catalog = new Catalog(
            schemaVersion: 1,
            catalogId: CatalogId.Create("sample-app"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("fi")],
            defaultSyntaxProfile: MessageSyntaxProfile.Plain);
        var import = new ImportSourceStringsUseCase(new SourceStringsJsonReader(), new MergeSourceStringsUseCase());
        var fixture = GetFixturePath("source-strings-sample.json");

        var first = await import.ExecuteAsync(catalog, fixture);
        var second = await import.ExecuteAsync(catalog, fixture);

        Assert.Multiple(() =>
        {
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.AddedCount, Is.EqualTo(4));
            Assert.That(second.AddedCount, Is.EqualTo(0));
            Assert.That(second.UpdatedCount, Is.EqualTo(0));
            Assert.That(second.UnchangedCount, Is.EqualTo(4));
        });

        const string unitKey = "items.count.other";
        new SetTranslationDraftUseCase().Execute(catalog, new SetTranslationDraftRequest
        {
            Key = unitKey,
            Locale = "fi",
            Text = "{0} kohdetta"
        });
        new ApproveTranslationUseCase().Execute(catalog, new ApproveTranslationRequest
        {
            Key = unitKey,
            Locale = "fi"
        });

        var outputDirectory = TempDirectory.Create("localizer-source-strings-tests");
        await new CompactLocaleJsonExporter().ExportAsync(outputDirectory, catalog);

        var fiJson = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "fi.json"));
        Assert.That(fiJson, Is.EqualTo("{\"items.count.other\":\"{0} kohdetta\"}\n"));
    }

    [Test]
    public async Task Import_ReportsReaderFailureAsOutcomeWithoutChangingCatalog()
    {
        var catalog = new Catalog(
            schemaVersion: 1,
            catalogId: CatalogId.Create("sample-app"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("fi")],
            defaultSyntaxProfile: MessageSyntaxProfile.Plain);
        var path = await WriteAsync("not json");

        var outcome = await new ImportSourceStringsUseCase(new SourceStringsJsonReader(), new MergeSourceStringsUseCase())
            .ExecuteAsync(catalog, path);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.ErrorCode, Is.EqualTo(CatalogPersistenceErrorCodes.Malformed));
            Assert.That(catalog.Entries, Is.Empty);
        });
    }

    private static async Task<string> WriteAsync(string json)
    {
        var path = Path.Combine(TempDirectory.Create("localizer-source-strings-tests"), "source.json");
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", fileName);
}
