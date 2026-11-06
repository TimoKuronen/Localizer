using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;

namespace Localizer.Desktop.Tests;

public sealed class ReviewWorkflowTests
{
    [AvaloniaTest]
    public void NextDraftKey_SkipsApprovedEntries()
    {
        var catalog = CreateCatalog(["es"], ("a", "es"), ("c", "es"));
        AddApproved(catalog, "b", "es");
        using var window = ReviewWindowHarness.Open(catalog);

        window.Press(PhysicalKey.F8);

        Assert.That(window.SelectedKey, Is.EqualTo("c"));
        Assert.That(window.ReviewTarget, Is.EqualTo("es"));
    }

    [AvaloniaTest]
    public void PreviousDraftKey_WrapsToLastDraft()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es"], ("a", "es"), ("b", "es")));

        window.Press(PhysicalKey.F8, RawInputModifiers.Shift);

        Assert.That(window.SelectedKey, Is.EqualTo("b"));
    }

    [AvaloniaTest]
    public void NextAndApproveKeys_ClearEveryDraft()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es"], ("a", "es"), ("b", "es"), ("c", "es")));

        for (var index = 0; index < 3; index++)
        {
            window.Press(PhysicalKey.F9);
            window.Press(PhysicalKey.F8);
        }

        Assert.That(window.ViewModel.SummaryText, Does.Contain("Draft 0").And.Contain("Approved 3"));
        Assert.That(window.ViewModel.StatusText, Is.EqualTo("No drafts left to review."));
    }

    [AvaloniaTest]
    public void NextDraftKey_VisitsEachLocaleOfAnEntry()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es", "fr"], ("a", "es"), ("a", "fr")));
        Assert.That(window.ReviewTarget, Is.EqualTo("es"));

        window.Press(PhysicalKey.F9);
        Assert.That(window.Translation("es").IsApproved, Is.True);
        Assert.That(window.Translation("fr").IsDraft, Is.True);

        window.Press(PhysicalKey.F8);
        Assert.That(window.ReviewTarget, Is.EqualTo("fr"));
    }

    [AvaloniaTest]
    public void ApproveKey_DoesNotApproveTwiceInARowAcrossLocales()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es", "fr"], ("a", "es"), ("a", "fr")));

        window.Press(PhysicalKey.F9);
        window.Press(PhysicalKey.F9);

        Assert.That(window.Translation("fr").IsDraft, Is.True);
    }

    [AvaloniaTest]
    public void ApproveKey_TargetsTheLocaleBeingEdited()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es", "fr"], ("a", "es"), ("a", "fr")));

        window.ClickTranslationText("fr");
        window.Press(PhysicalKey.F9);

        Assert.Multiple(() =>
        {
            Assert.That(window.Translation("fr").IsApproved, Is.True);
            Assert.That(window.Translation("es").IsDraft, Is.True);
        });
    }

    [AvaloniaTest]
    public void DownArrow_MovesToSecondEntryRightAfterOpening()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es"], ("a", "es"), ("b", "es")));
        Assert.That(window.SelectedKey, Is.EqualTo("a"));

        window.Press(PhysicalKey.ArrowDown);

        Assert.That(window.SelectedKey, Is.EqualTo("b"));
    }

    [AvaloniaTest]
    public void DownArrow_MovesToNextEntryAfterApprovingFirstEntryWithoutClicking()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es"], ("a", "es"), ("b", "es")));

        window.Press(PhysicalKey.F9);
        window.Press(PhysicalKey.ArrowDown);

        Assert.That(window.SelectedKey, Is.EqualTo("b"));
    }

    [AvaloniaTest]
    public void DownArrow_MovesToNextEntryAfterReviewingBothLocales()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(
            ["es", "fr"],
            ("a", "es"),
            ("a", "fr"),
            ("b", "es"),
            ("b", "fr")));

        window.Press(PhysicalKey.F9);
        window.Press(PhysicalKey.F8);
        window.Press(PhysicalKey.F9);
        window.Press(PhysicalKey.ArrowDown);

        Assert.That(window.SelectedKey, Is.EqualTo("b"));
    }

    [AvaloniaTest]
    public void DownArrow_MovesToNextEntryAfterApproveKey()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es"], ("a", "es"), ("b", "es"), ("c", "es")));
        window.Press(PhysicalKey.F8);
        Assert.That(window.SelectedKey, Is.EqualTo("b"));

        window.Press(PhysicalKey.F9);
        window.Press(PhysicalKey.ArrowDown);

        Assert.That(window.SelectedKey, Is.EqualTo("c"));
    }

    [AvaloniaTest]
    public void DownArrow_MovesToNextEntryAfterClickingApprove()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es"], ("a", "es"), ("b", "es")));

        window.ClickApprove("es");
        Assert.That(window.Translation("es").IsApproved, Is.True);
        Assert.That(window.FocusedElement, Is.Not.Null);

        window.Press(PhysicalKey.ArrowDown);

        Assert.That(window.SelectedKey, Is.EqualTo("b"));
    }

    [AvaloniaTest]
    public void DownArrow_StillWorksAfterBlockedApproval()
    {
        var catalog = CreateCatalog(["es"], MessageSyntaxProfile.Composite);
        AddDraft(catalog, "a", "es", "Coins {0}", "Monedas");
        AddDraft(catalog, "b", "es", "Start", "Empezar");
        using var window = ReviewWindowHarness.Open(catalog);

        window.Press(PhysicalKey.F9);
        Assert.That(window.Messages.Single(), Does.StartWith("Approve failed"));
        Assert.That(window.Translation("es").IsDraft, Is.True);

        window.Press(PhysicalKey.ArrowDown);

        Assert.That(window.SelectedKey, Is.EqualTo("b"));
    }

    [AvaloniaTest]
    public void Approve_KeepsUnappliedEditsToOtherLocales()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es", "fr"], ("a", "es"), ("a", "fr")));

        window.ClickTranslationText("fr");
        window.Type("X");
        var editedFrench = window.Translation("fr").Text;
        Assert.That(editedFrench, Does.Contain("X"));

        window.ClickApprove("es");

        Assert.That(window.Translation("fr").Text, Is.EqualTo(editedFrench));
        Assert.That(window.Translation("es").IsApproved, Is.True);
    }

    [AvaloniaTest]
    public void Approve_WorksDirectlyOnEditedApprovedText()
    {
        var catalog = CreateCatalog(["es"]);
        AddApproved(catalog, "a", "es");
        using var window = ReviewWindowHarness.Open(catalog);
        Assert.That(window.Translation("es").CanApprove, Is.False);

        window.ClickTranslationText("es");
        window.Type("X");
        var edited = window.Translation("es").Text;
        Assert.That(window.Translation("es").CanApprove, Is.True);

        window.Press(PhysicalKey.F9);

        Assert.That(window.Translation("es").IsApproved, Is.True);
        Assert.That(window.Translation("es").Text, Is.EqualTo(edited));
    }

    [AvaloniaTest]
    public void Typing_EnablesSaveAndSaveWritesTheEdit()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es"], ("a", "es")));
        Assert.That(window.ViewModel.CanSave, Is.False);

        window.ClickTranslationText("es");
        window.Type("X");
        var edited = window.Translation("es").Text;
        Assert.That(window.ViewModel.CanSave, Is.True);
        Assert.That(window.ViewModel.WindowTitle, Does.EndWith("*"));

        window.Press(PhysicalKey.S, RawInputModifiers.Control);
        window.PumpUntil(() => !window.ViewModel.IsDirty);

        var saved = window.ReadSavedCatalog().Entries[EntryKey.Create("a")].Translations[Locale.Create("es")];
        Assert.That(saved.Text, Is.EqualTo(edited));
        Assert.That(window.ViewModel.WindowTitle, Does.Not.EndWith("*"));
    }

    [AvaloniaTest]
    public void Save_KeepsFocusInTheTextBeingEdited()
    {
        using var window = ReviewWindowHarness.Open(CreateCatalog(["es"], ("a", "es")));

        window.ClickTranslationText("es");
        var box = window.TranslationTextBox("es");
        window.Type("X");

        window.Press(PhysicalKey.S, RawInputModifiers.Control);
        window.PumpUntil(() => !window.ViewModel.IsBusy && !window.ViewModel.IsDirty);

        Assert.That(window.FocusedElement, Is.SameAs(box));
    }

    [AvaloniaTest]
    public void Drafting_ReturnsFocusToTheListWhenTheEditorIsRebuilt()
    {
        var catalog = CreateCatalog(["es"]);
        AddEntry(catalog, "a", "Start");
        AddEntry(catalog, "b", "Stop");
        using var window = ReviewWindowHarness.Open(catalog);

        window.ClickTranslationText("es");
        window.ViewModel.DraftMissingAndStaleCommand.Execute(null);
        window.PumpUntil(() => !window.ViewModel.IsBusy);
        window.Press(PhysicalKey.ArrowDown);

        Assert.That(window.SelectedKey, Is.EqualTo("b"));
    }

    [AvaloniaTest]
    public void Drafting_KeepsTextTypedBeforeIt()
    {
        var catalog = CreateCatalog(["es"]);
        AddEntry(catalog, "a", "Start");
        using var window = ReviewWindowHarness.Open(catalog);

        window.ClickTranslationText("es");
        window.Type("Empezar");
        window.ViewModel.DraftMissingAndStaleCommand.Execute(null);
        window.PumpUntil(() => !window.ViewModel.IsBusy);

        Assert.That(window.Translation("es").Text, Is.EqualTo("Empezar"));
        Assert.That(window.Translation("es").IsDraft, Is.True);
    }

    [AvaloniaTest]
    public void MovingToAnotherEntry_UpdatesSummaryForCommittedEdit()
    {
        var catalog = CreateCatalog(["es"]);
        AddEntry(catalog, "a", "Start");
        AddEntry(catalog, "b", "Stop");
        using var window = ReviewWindowHarness.Open(catalog);
        Assert.That(window.ViewModel.SummaryText, Does.Contain("Missing 2"));

        window.ClickTranslationText("es");
        window.Type("Empezar");
        window.ViewModel.SelectedEntry = window.ViewModel.Entries[1];
        window.Pump();

        Assert.That(window.ViewModel.SummaryText, Does.Contain("Missing 1").And.Contain("Draft 1"));
    }

    private static Catalog CreateCatalog(string[] locales, params (string Key, string Locale)[] drafts)
    {
        var catalog = CreateCatalog(locales, MessageSyntaxProfile.Plain);
        foreach (var (key, locale) in drafts)
        {
            AddDraft(catalog, key, locale, $"Source {key}", $"Text {key} {locale}");
        }

        return catalog;
    }

    private static Catalog CreateCatalog(string[] locales, MessageSyntaxProfile syntax) =>
        new CreateCatalogUseCase().Execute(new CreateCatalogRequest
        {
            CatalogId = "catalog",
            SourceLocale = "en",
            RequiredLocales = locales,
            DefaultSyntaxProfile = syntax
        }).Value!;

    private static void AddApproved(Catalog catalog, string key, string locale)
    {
        AddDraft(catalog, key, locale, $"Source {key}", $"Text {key} {locale}");
        var approved = new ApproveTranslationUseCase().Execute(
            catalog,
            new ApproveTranslationRequest { Key = key, Locale = locale });
        Assert.That(approved.Succeeded, Is.True, approved.ErrorMessage);
    }

    private static void AddDraft(Catalog catalog, string key, string locale, string source, string text)
    {
        if (!catalog.Entries.ContainsKey(EntryKey.Create(key)))
        {
            AddEntry(catalog, key, source);
        }

        var drafted = new SetTranslationDraftUseCase().Execute(
            catalog,
            new SetTranslationDraftRequest { Key = key, Locale = locale, Text = text });
        Assert.That(drafted.Succeeded, Is.True, drafted.ErrorMessage);
    }

    private static void AddEntry(Catalog catalog, string key, string source)
    {
        var added = new AddCatalogEntryUseCase().Execute(
            catalog,
            new AddCatalogEntryRequest { Key = key, SourceText = source });
        Assert.That(added.Succeeded, Is.True, added.ErrorMessage);
    }
}
