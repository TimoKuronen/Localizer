using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Localizer.Desktop.Tests.Fakes;
using Localizer.Desktop.ViewModels;
using Localizer.Desktop.Views;

namespace Localizer.Desktop.Tests;

public sealed class WorkflowGuideTests
{
    private static readonly Regex MenuPath = new(
        @"\b(File|Entry|Review|Project|Export) > ([A-Za-z/ \-]+(?:\.\.\.)?)",
        RegexOptions.Compiled);

    [Test]
    public void Guide_OffersSetupAndEditSectionsWithNumberedSingleSentenceSteps()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WorkflowGuide.All, Has.Count.EqualTo(2));
            Assert.That(WorkflowGuide.All[0].Title, Does.StartWith("A."));
            Assert.That(WorkflowGuide.All[1].Title, Does.StartWith("B."));

            foreach (var section in WorkflowGuide.All)
            {
                Assert.That(
                    section.Steps.Select(step => step.Number),
                    Is.EqualTo(Enumerable.Range(1, section.Steps.Count)));
                Assert.That(section.Steps.All(step => step.Text.EndsWith('.')), Is.True);
            }
        });
    }

    [AvaloniaTest]
    public void Guide_NamesOnlyMenuItemsThatExist()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));
        var menuPaths = window.Window.GetLogicalDescendants()
            .OfType<MenuItem>()
            .Where(top => top.ItemCount > 0)
            .SelectMany(top => top.Items.OfType<MenuItem>().Select(item =>
                $"{Plain(top.Header)} > {Plain(item.Header)}"))
            .ToList();

        var mentioned = WorkflowGuide.All
            .SelectMany(section => section.Steps)
            .SelectMany(step => MenuPath.Matches(step.Text).Select(match => match.Value.Trim()))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(mentioned, Is.Not.Empty);
            Assert.That(
                mentioned.Where(path => !menuPaths.Any(known => path.StartsWith(known, StringComparison.Ordinal))),
                Is.Empty);
        });
    }

    [AvaloniaTest]
    public void HelpWorkflowGuide_OpensTheGuideFromTheSharedSource()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));

        fixture.ViewModel.ShowWorkflowGuideCommand.Execute(null);
        fixture.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.WorkflowGuideShownCount, Is.EqualTo(1));
            Assert.That(fixture.Dialogs.ShownWorkflowGuide, Is.SameAs(WorkflowGuide.All));
        });
    }

    [AvaloniaTest]
    public void WorkflowGuideWindow_ListsEveryStep()
    {
        var window = new WorkflowGuideWindow { DataContext = new WorkflowGuideViewModel(WorkflowGuide.All) };
        window.Show();

        var shown = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text)
            .ToList();

        Assert.Multiple(() =>
        {
            foreach (var section in WorkflowGuide.All)
            {
                Assert.That(shown, Does.Contain(section.Title));
                Assert.That(section.Steps.Select(step => step.Text).Except(shown), Is.Empty);
            }
        });
        window.Close();
    }

    private static string Plain(object? header) =>
        (header?.ToString() ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal);
}
