using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class WindowBindingTests
{
    [AvaloniaTest]
    public void EveryMenuItemIsBoundToACommand()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));

        var unbound = window.Window.GetLogicalDescendants()
            .OfType<MenuItem>()
            .Where(item => item.ItemCount == 0 && item.Command is null)
            .Select(item => item.Header?.ToString())
            .ToList();

        Assert.That(unbound, Is.Empty);
    }

    [AvaloniaTest]
    public void MenuBarOffersTheExpectedMenus()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));

        var topLevel = window.Window.GetLogicalDescendants()
            .OfType<Menu>()
            .Single()
            .Items.OfType<MenuItem>()
            .Select(item => item.Header?.ToString())
            .ToList();

        Assert.That(topLevel, Is.EqualTo(new[] { "_File", "_Entry", "_Review", "_Project", "E_xport" }));
    }

    [AvaloniaTest]
    public void SaveButtonAndMenuItemFollowCanSave()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));
        var button = ToolbarButton(window, "Save");
        var menuItem = MenuItemByHeader(window, "_Save");
        Assert.That(button.IsEnabled, Is.False);
        Assert.That(menuItem.IsEnabled, Is.False);

        window.ClickTranslationText("es");
        window.Type("X");

        Assert.That(button.IsEnabled, Is.True);
        Assert.That(menuItem.IsEnabled, Is.True);
    }

    [AvaloniaTest]
    public void WithoutAProjectFolder_ToolbarOffersJsonExportOnly()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));

        Assert.Multiple(() =>
        {
            Assert.That(ToolbarButton(window, "Export JSON").IsVisible, Is.True);
            Assert.That(ToolbarButton(window, "Import from Project").IsVisible, Is.False);
            Assert.That(ToolbarButton(window, "Export to Project").IsVisible, Is.False);
        });
    }

    [AvaloniaTest]
    public void CancelButton_AppearsOnlyWhileBusy()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));
        var cancel = ToolbarButton(window, "Cancel");
        Assert.That(cancel.IsVisible, Is.False);

        window.ViewModel.IsBusy = true;
        window.Pump();
        Assert.That(cancel.IsVisible, Is.True);
        Assert.That(ToolbarButton(window, "Draft Missing/Stale").IsEnabled, Is.False);

        window.ViewModel.IsBusy = false;
        window.Pump();
        Assert.That(cancel.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public void ApproveButton_FollowsTheRowState()
    {
        var catalog = CatalogBuilder.WithEntries(["es"], "a");
        CatalogBuilder.AddDraft(catalog, "a", "es", "Hola");
        using var window = ReviewWindowHarness.Open(catalog);
        var approve = window.Window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content as string == "Approve");
        Assert.That(approve.IsEnabled, Is.True);

        window.Press(PhysicalKey.F9);

        Assert.That(approve.IsEnabled, Is.False);
    }

    private static Button ToolbarButton(ReviewWindowHarness window, string content) =>
        window.Window.GetLogicalDescendants()
            .OfType<Button>()
            .Single(button => button.Content as string == content);

    private static MenuItem MenuItemByHeader(ReviewWindowHarness window, string header) =>
        window.Window.GetLogicalDescendants()
            .OfType<MenuItem>()
            .Single(item => item.Header as string == header);
}
