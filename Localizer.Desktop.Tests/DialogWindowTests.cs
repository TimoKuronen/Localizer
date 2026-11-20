using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using Localizer.Application.UseCases;
using Localizer.Core.Syntax;
using Localizer.Desktop.ViewModels;
using Localizer.Desktop.Views;

namespace Localizer.Desktop.Tests;

public sealed class DialogWindowTests
{
    [AvaloniaTest]
    public void ProjectFolderWindow_SaveFollowsTheViewModel()
    {
        var model = new ProjectFolderViewModel("folder", ["UI.csv"], null, null);
        var window = new ProjectFolderWindow { DataContext = model };
        window.Show();
        var save = ButtonWithContent(window, "Save");
        Assert.That(save.IsEnabled, Is.True);

        model.ImportFileName = string.Empty;

        Assert.That(save.IsEnabled, Is.False);
        window.Close();
    }

    [AvaloniaTest]
    public void ProjectFolderWindow_ExportBoxIsEditableOnlyWithoutMirroring()
    {
        var model = new ProjectFolderViewModel("folder", ["UI.csv"], null, null);
        var window = new ProjectFolderWindow { DataContext = model };
        window.Show();
        var exportBox = window.GetVisualDescendants()
            .OfType<TextBox>()
            .Single(box => !box.IsReadOnly && !box.IsEnabled);

        model.MirrorExport = false;

        Assert.That(exportBox.IsEnabled, Is.True);
        window.Close();
    }

    [AvaloniaTest]
    public void NewCatalogWindow_ShowsItsDefaults()
    {
        var window = new NewCatalogWindow { DataContext = new NewCatalogViewModel() };
        window.Show();

        var texts = window.GetVisualDescendants().OfType<TextBox>().Select(box => box.Text).ToList();

        Assert.That(texts, Is.EqualTo(new[] { "catalog", "en", "es" }));
        Assert.That(ButtonWithContent(window, "Create"), Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void AddEntryWindow_BindsItsFields()
    {
        var model = new AddEntryViewModel { Key = "ui.start" };
        var window = new AddEntryWindow { DataContext = model };
        window.Show();

        var texts = window.GetVisualDescendants().OfType<TextBox>().Select(box => box.Text).ToList();

        Assert.That(texts, Does.Contain("ui.start"));
        Assert.That(ButtonWithContent(window, "Add"), Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void MessageWindow_ShowsTheMessage()
    {
        var window = new MessageWindow
        {
            Title = "Approve failed",
            DataContext = new MessageViewModel { Message = "Text is empty." }
        };
        window.Show();

        var shown = window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

        Assert.That(shown, Does.Contain("Text is empty."));
        window.Close();
    }

    [AvaloniaTest]
    public void CatalogSettingsWindow_ShowsTheCurrentSettings()
    {
        var model = new CatalogSettingsViewModel(new ConfigureCatalogSettingsRequest
        {
            SourceLocale = "en",
            RequiredLocales = ["es", "fr"],
            DefaultSyntaxProfile = MessageSyntaxProfile.Composite,
            ProjectContext = "A calm habit app.",
            Glossary = "fr: habit = habitude"
        });
        var window = new CatalogSettingsWindow { DataContext = model };
        window.Show();

        var texts = window.GetVisualDescendants().OfType<TextBox>().Select(box => box.Text).ToList();
        var composite = window.GetVisualDescendants().OfType<CheckBox>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                texts,
                Is.EqualTo(new[] { "en", "es, fr", "A calm habit app.", "fr: habit = habitude" }));
            Assert.That(composite.IsChecked, Is.True);
            Assert.That(ButtonWithContent(window, "Apply"), Is.Not.Null);
        });
        window.Close();
    }

    [AvaloniaTest]
    public void DraftModelWindow_OffersAutomaticAndInstalledModels()
    {
        var model = new DraftModelViewModel(["qwen3.5:4b", "mistral:latest"], "mistral:latest");
        var window = new DraftModelWindow { DataContext = model };
        window.Show();

        var combo = window.GetVisualDescendants().OfType<ComboBox>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(combo.ItemCount, Is.EqualTo(3));
            Assert.That(combo.SelectedItem, Is.EqualTo("mistral:latest"));
            Assert.That(model.SelectedModelName, Is.EqualTo("mistral:latest"));
            Assert.That(ButtonWithContent(window, "Use"), Is.Not.Null);
        });
        window.Close();
    }

    [AvaloniaTest]
    public void DraftModelViewModel_FallsBackToAutomaticWhenTheSavedModelIsGone()
    {
        var model = new DraftModelViewModel(["qwen3.5:4b"], "removed:latest");

        Assert.That(model.SelectedModelName, Is.Null);
    }

    [AvaloniaTest]
    public void UnsavedChangesWindow_OffersSaveDontSaveAndCancel()
    {
        var window = new UnsavedChangesWindow
        {
            DataContext = new MessageViewModel { Message = "Save changes?" }
        };
        window.Show();

        var buttons = window.GetVisualDescendants().OfType<Button>().Select(button => button.Content).ToList();

        Assert.That(buttons, Is.EqualTo(new object[] { "Save", "Don't Save", "Cancel" }));
        window.Close();
    }

    private static Button ButtonWithContent(Window window, string content) =>
        window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content as string == content);
}
