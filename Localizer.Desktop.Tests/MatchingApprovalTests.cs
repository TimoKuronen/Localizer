using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Localizer.Desktop.Tests.Fakes;
using Localizer.Desktop.ViewModels;
using Localizer.Desktop.Views;

namespace Localizer.Desktop.Tests;

public sealed class MatchingApprovalTests
{
    [AvaloniaTest]
    public void ApproveKey_OffersRepeatsAndApprovesTheTickedOnes()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog());
        window.Dialogs.ChecklistAnswer = checklist => checklist.SelectedIds;
        Assert.That(window.ViewModel.Entries.Select(entry => entry.SameSource), Is.EqualTo(new[] { "x2", "x2", "" }));

        window.Press(PhysicalKey.F9);

        Assert.That(window.Dialogs.Checklists.Single().Items.Single().Id, Is.EqualTo("b"));
        Assert.That(window.ViewModel.SummaryText, Does.Contain("Approved 2"));
        Assert.That(window.ViewModel.Entries[1].LocaleStatuses, Is.EqualTo("es:Approved"));
    }

    [AvaloniaTest]
    public void SkippingTheOffer_ApprovesOnlyTheHighlightedTranslation()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog());

        window.Press(PhysicalKey.F9);

        Assert.That(window.ViewModel.SummaryText, Does.Contain("Approved 1"));
    }

    [AvaloniaTest]
    public void ChecklistWindow_ConfirmTextFollowsTheTicks()
    {
        var checklist = new ChecklistViewModel(
            "Title", "Message", "Remove", "Cancel", false, [new ChecklistItemViewModel("a", "detail", true)]);
        var window = new ChecklistWindow { DataContext = checklist };
        window.Show();
        var confirm = window.FindControl<Button>("ConfirmButton")!;
        Assert.That(confirm.Content, Is.EqualTo("Remove 1 selected"));
        Assert.That(window.FocusManager?.GetFocusedElement(), Is.SameAs(window.FindControl<Button>("CancelButton")));

        checklist.Items[0].IsSelected = false;

        Assert.That(confirm.IsEnabled, Is.False);
        window.Close();
    }

    private static Localizer.Core.Catalogs.Catalog CreateCatalog()
    {
        var catalog = CatalogBuilder.Create(["es"]);
        CatalogBuilder.AddEntry(catalog, "a", "Open");
        CatalogBuilder.AddEntry(catalog, "b", "Open");
        CatalogBuilder.AddEntry(catalog, "c", "Close");
        CatalogBuilder.AddDraft(catalog, "a", "es", "Abrir");
        return catalog;
    }
}
