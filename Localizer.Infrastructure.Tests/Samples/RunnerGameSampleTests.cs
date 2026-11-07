using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Validation;
using Localizer.Infrastructure.Export;
using Localizer.Infrastructure.Import;
using Localizer.Infrastructure.Persistence.Json;

namespace Localizer.Infrastructure.Tests.Samples;

public sealed class RunnerGameSampleTests
{
    [Test]
    public async Task Catalog_CoversEveryReviewState()
    {
        var catalog = await OpenSampleAsync();

        var summary = new GetCatalogStatusSummaryUseCase().Execute(catalog);

        Assert.Multiple(() =>
        {
            Assert.That(summary.EntryCount, Is.EqualTo(14));
            Assert.That(summary.MissingCount, Is.Zero);
            Assert.That(summary.StaleCount, Is.EqualTo(1));
            Assert.That(summary.DraftCount, Is.EqualTo(9));
            Assert.That(summary.ApprovedCount, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task Catalog_HasOneDraftWithBlockingPlaceholderErrors()
    {
        var catalog = await OpenSampleAsync();

        var diagnostics = new ValidateCatalogUseCase().Execute(catalog).Diagnostics;

        Assert.That(diagnostics, Has.Count.EqualTo(2));
        Assert.That(diagnostics.Select(diagnostic => diagnostic.Code), Is.All.EqualTo(DiagnosticCodes.SyntaxPlaceholderMissing));
    }

    [Test]
    public async Task ProjectCsv_ImportsOneNewEntryAndExportsApprovedText()
    {
        var catalog = await OpenSampleAsync();
        var directory = Directory.CreateTempSubdirectory("localizer-sample-tests-").FullName;

        try
        {
            var imported = await new ImportUnityCsvUseCase(new UnityCsvReader(), new MergeUnityCsvImportUseCase())
                .ExecuteAsync(catalog, GetSamplePath("project", "UI.csv"));
            Assert.That(imported.Succeeded, Is.True, imported.ErrorMessage);
            Assert.That(imported.MergeOutcome.AddedCount, Is.EqualTo(1));

            var exportPath = Path.Combine(directory, "UI.export.csv");
            var exported = await new ExportUnityCsvUseCase(new UnityCsvExporter()).ExecuteAsync(catalog, exportPath);
            Assert.That(exported.Succeeded, Is.True, exported.ErrorMessage);

            var csv = await File.ReadAllTextAsync(exportPath);
            Assert.That(csv, Does.Contain("ui.menu.start,10000000107,Start,Empezar"));
            Assert.That(csv, Does.Contain("ui.results.rank,10000000115,Rank: {0},"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<Catalog> OpenSampleAsync()
    {
        var result = await new OpenCatalogUseCase(new JsonCatalogStore())
            .ExecuteAsync(GetSamplePath("runner-game.json"));

        Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
        return result.Value!;
    }

    private static string GetSamplePath(params string[] segments) =>
        Path.Combine([TestContext.CurrentContext.TestDirectory, "Samples", "runner-game", .. segments]);
}
