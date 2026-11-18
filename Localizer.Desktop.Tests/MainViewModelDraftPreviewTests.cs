using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Localizer.Application.UseCases;
using Localizer.Desktop.Tests.Fakes;
using Localizer.Desktop.ViewModels;

namespace Localizer.Desktop.Tests;

public sealed class MainViewModelDraftPreviewTests
{
    [AvaloniaTest]
    public void DraftPreview_WritesDraftTextShowsTheReportAndLeavesTheCatalogClean()
    {
        var catalog = CatalogBuilder.WithEntries(["es"], "ui.start", "ui.quit");
        CatalogBuilder.AddDraft(catalog, "ui.start", "es", "Empezar");
        var fixture = MainViewModelFixture.Open(catalog);
        var directory = Directory.CreateTempSubdirectory("localizer-preview-vm-").FullName;
        fixture.Dialogs.PreviewDirectory = directory;
        fixture.ViewModel.IsDirty = false;

        fixture.ViewModel.ExportDraftPreviewCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.Reports.Count > 0);

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(directory, "es.json")), Is.EqualTo("{\"ui.start\":\"Empezar\"}\n"));
            Assert.That(fixture.ViewModel.StatusText, Is.EqualTo($"Draft preview written to {directory}."));
            Assert.That(fixture.Dialogs.Reports[0], Does.StartWith("Draft preview report:"));
            Assert.That(fixture.Dialogs.Reports[0], Does.Contain("es: 1 included (Approved 0, Draft 1, Stale 0)"));
            Assert.That(fixture.Dialogs.Reports[0], Does.Contain("es  ui.quit  [Missing]"));
            Assert.That(fixture.ViewModel.IsDirty, Is.False);
        });
    }

    [AvaloniaTest]
    public void DraftPreview_DoesNothingWhenTheFolderPickerIsCancelled()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "ui.start"));
        fixture.Dialogs.PreviewDirectory = null;

        fixture.ViewModel.ExportDraftPreviewCommand.Execute(null);
        fixture.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.Reports, Is.Empty);
            Assert.That(fixture.Dialogs.Messages, Is.Empty);
        });
    }

    [AvaloniaTest]
    public void HelpShortcuts_OpensTheShortcutListFromTheSharedSource()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));

        fixture.ViewModel.ShowKeyboardShortcutsCommand.Execute(null);
        fixture.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.ShortcutsShownCount, Is.EqualTo(1));
            Assert.That(fixture.Dialogs.ShownShortcuts, Is.SameAs(KeyboardShortcuts.All));
        });
    }

    [AvaloniaTest]
    public void ShortcutList_CoversEveryWindowKeyBindingAndMenuGesture()
    {
        using var window = ReviewWindowHarness.Open(CatalogBuilder.WithEntries(["es"], "a"));
        var listed = KeyboardShortcuts.All.Select(shortcut => KeyGesture.Parse(shortcut.Keys)).ToList();

        var bound = window.Window.KeyBindings.Select(binding => binding.Gesture).ToList();
        var menuGestures = window.Window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Select(item => item.InputGesture)
            .Where(gesture => gesture is not null)
            .Cast<KeyGesture>()
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(bound, Is.Not.Empty);
            Assert.That(bound.Where(gesture => !listed.Contains(gesture)), Is.Empty);
            Assert.That(menuGestures.Where(gesture => !listed.Contains(gesture)), Is.Empty);
            Assert.That(KeyboardShortcuts.All.All(shortcut => shortcut.Action.Length > 0), Is.True);
        });
    }

    [Test]
    public void ReportText_SummarizesEachLocaleAndListsWhatWasLeftOut()
    {
        var outcome = new ExportDraftPreviewOutcome
        {
            Succeeded = true,
            WrittenFiles = [],
            Report = new DraftPreviewReport
            {
                LocaleSummaries =
                [
                    new DraftPreviewLocaleSummary
                    {
                        Locale = "fi",
                        ApprovedCount = 1,
                        DraftCount = 2,
                        StaleCount = 3,
                        MissingCount = 4,
                        InvalidCount = 5
                    }
                ],
                SkippedItems =
                [
                    new DraftPreviewSkippedItem
                    {
                        Key = "a",
                        Locale = "fi",
                        Reason = DraftPreviewSkippedItem.SkipReason.InvalidTranslation,
                        Message = "bad"
                    }
                ]
            }
        };

        var text = DraftPreviewReportText.Build(outcome, "out");

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("fi: 6 included (Approved 1, Draft 2, Stale 3); left out: Missing 4, Invalid 5"));
            Assert.That(text, Does.Contain("fi  a  [Invalid] bad"));
            Assert.That(text, Does.Contain("Preview only"));
        });
    }
}
