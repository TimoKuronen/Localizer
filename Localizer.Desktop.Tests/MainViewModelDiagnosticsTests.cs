using Avalonia.Headless.NUnit;
using Localizer.Core.Syntax;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class MainViewModelDiagnosticsTests
{
    [AvaloniaTest]
    public void CleanCatalog_ReportsNoDiagnostics()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.Diagnostics, Is.Empty);
            Assert.That(fixture.ViewModel.DiagnosticsHeaderText, Is.EqualTo("Diagnostics: none"));
        });
    }

    [AvaloniaTest]
    public void PlaceholderMismatch_CountsErrorsInTheHeader()
    {
        var catalog = CatalogBuilder.Create(["es"], MessageSyntaxProfile.Composite);
        CatalogBuilder.AddEntry(catalog, "a", "Coins {0}");
        CatalogBuilder.AddDraft(catalog, "a", "es", "Monedas");

        var fixture = MainViewModelFixture.Open(catalog);

        var errors = fixture.ViewModel.Diagnostics.Count(row => row.Severity == "Error");
        Assert.That(errors, Is.GreaterThan(0));
        Assert.That(
            fixture.ViewModel.DiagnosticsHeaderText,
            Does.StartWith(errors == 1 ? "Diagnostics: 1 error" : $"Diagnostics: {errors} errors"));
    }

    [AvaloniaTest]
    public void Summary_CountsEntriesByStatus()
    {
        var catalog = CatalogBuilder.WithEntries(["es"], "a", "b", "c");
        CatalogBuilder.AddDraft(catalog, "a", "es", "Hola");

        var fixture = MainViewModelFixture.Open(catalog);

        Assert.That(
            fixture.ViewModel.SummaryText,
            Is.EqualTo("Entries 3 | Missing 2 | Stale 0 | Draft 1 | Approved 0"));
    }
}
