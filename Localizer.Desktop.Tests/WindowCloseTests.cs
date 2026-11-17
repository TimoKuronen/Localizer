using Avalonia.Headless.NUnit;
using Localizer.Desktop.Services;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class WindowCloseTests
{
    [AvaloniaTest]
    public void Close_OnACleanCatalog_ClosesWithoutPrompt()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));

        window.Window.Close();
        window.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(window.Window.IsVisible, Is.False);
            Assert.That(window.Dialogs.UnsavedPromptCount, Is.Zero);
        });
    }

    [AvaloniaTest]
    public void Close_WithUnsavedChanges_Cancel_KeepsTheWindowOpen()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));
        window.Translation("es").Text = "Hola";
        window.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        window.Window.Close();
        window.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(window.Window.IsVisible, Is.True);
            Assert.That(window.Dialogs.UnsavedPromptCount, Is.EqualTo(1));
        });
    }

    [AvaloniaTest]
    public void Close_WithUnsavedChanges_Discard_ClosesWithoutSaving()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));
        var before = File.GetLastWriteTimeUtc(window.CatalogPath);
        window.Translation("es").Text = "Hola";
        window.Dialogs.UnsavedChoice = UnsavedChangesChoice.Discard;

        window.Window.Close();
        window.PumpUntil(() => !window.Window.IsVisible);

        Assert.That(File.GetLastWriteTimeUtc(window.CatalogPath), Is.EqualTo(before));
    }

    [AvaloniaTest]
    public void Close_WithUnsavedChanges_Save_WritesTheCatalogThenCloses()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));
        window.Translation("es").Text = "Hola";
        window.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        window.Window.Close();
        window.PumpUntil(() => !window.Window.IsVisible);

        var saved = window.ReadSavedCatalog();
        Assert.That(
            saved.Entries.Values.Single().Translations.Values.Single().Text,
            Is.EqualTo("Hola"));
    }
}
