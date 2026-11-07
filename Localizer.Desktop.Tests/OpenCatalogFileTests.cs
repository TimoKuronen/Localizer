using Avalonia.Headless.NUnit;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Syntax;

namespace Localizer.Desktop.Tests;

public sealed class OpenCatalogFileTests
{
    [AvaloniaTest]
    public void OpenCatalogFile_LoadsTheGivenPathWithoutAskingForOne()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog());
        window.ViewModel.StatusText = string.Empty;

        _ = window.ViewModel.OpenCatalogFileAsync(window.CatalogPath);
        window.PumpUntil(() => window.ViewModel.StatusText.Length > 0);

        Assert.That(window.ViewModel.StatusText, Is.EqualTo($"Opened {window.CatalogPath}"));
        Assert.That(window.ViewModel.Entries, Has.Count.EqualTo(1));
    }

    [AvaloniaTest]
    public void OpenCatalogFile_ReportsAMissingFile()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog());

        _ = window.ViewModel.OpenCatalogFileAsync(window.CatalogPath + ".missing");
        window.PumpUntil(() => window.Messages.Count > 0);

        Assert.That(window.Messages[0], Does.StartWith("Open catalog failed"));
    }

    private static Catalog CreateCatalog()
    {
        var catalog = new CreateCatalogUseCase().Execute(new CreateCatalogRequest
        {
            CatalogId = "catalog",
            SourceLocale = "en",
            RequiredLocales = ["es"],
            DefaultSyntaxProfile = MessageSyntaxProfile.Plain
        }).Value!;

        var added = new AddCatalogEntryUseCase().Execute(
            catalog,
            new AddCatalogEntryRequest { Key = "a", SourceText = "Source" });
        Assert.That(added.Succeeded, Is.True, added.ErrorMessage);

        return catalog;
    }
}
