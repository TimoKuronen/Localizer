using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class WindowShortcutTests
{
    [AvaloniaTest]
    public void CtrlN_PromptsForANewCatalog()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));

        window.Press(PhysicalKey.N, RawInputModifiers.Control);

        Assert.That(window.Dialogs.CreatePromptCount, Is.EqualTo(1));
    }

    [AvaloniaTest]
    public void CtrlO_AsksForACatalogToOpen()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));
        var picksBefore = window.Dialogs.OpenPickCount;

        window.Press(PhysicalKey.O, RawInputModifiers.Control);

        Assert.That(window.Dialogs.OpenPickCount, Is.EqualTo(picksBefore + 1));
    }

    [AvaloniaTest]
    public void CtrlShiftS_AsksWhereToSave()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));

        window.Press(PhysicalKey.S, RawInputModifiers.Control | RawInputModifiers.Shift);

        Assert.That(window.Dialogs.SavePickCount, Is.EqualTo(1));
    }

    [AvaloniaTest]
    public void CtrlS_OnACleanCatalog_DoesNotTouchTheFile()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));
        var before = File.GetLastWriteTimeUtc(window.CatalogPath);

        window.Press(PhysicalKey.S, RawInputModifiers.Control);

        Assert.That(File.GetLastWriteTimeUtc(window.CatalogPath), Is.EqualTo(before));
        Assert.That(window.Dialogs.SavePickCount, Is.Zero);
    }
}
