using Avalonia.Headless.NUnit;
using Localizer.Application.UseCases;
using Localizer.Core.Syntax;
using Localizer.Desktop.Services;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class MainViewModelSettingsTests
{
    [AvaloniaTest]
    public void CatalogSettings_PromptStartsFromTheCurrentValues()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es", "fr"], "a"));

        fixture.ViewModel.ConfigureCatalogCommand.Execute(null);
        fixture.Pump();

        var current = fixture.Dialogs.PromptedSettings;
        Assert.That(current, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(current!.SourceLocale, Is.EqualTo("en"));
            Assert.That(current.RequiredLocales, Is.EquivalentTo(new[] { "es", "fr" }));
            Assert.That(current.DefaultSyntaxProfile, Is.EqualTo(MessageSyntaxProfile.Plain));
        });
    }

    [AvaloniaTest]
    public void CatalogSettings_AddingALocale_ShowsItInTheEditorAndMarksDirty()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Dialogs.SettingsResult = Settings("en", ["es", "fr"], MessageSyntaxProfile.Plain);

        fixture.ViewModel.ConfigureCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.IsDirty);

        Assert.Multiple(() =>
        {
            Assert.That(
                fixture.ViewModel.Translations.Select(translation => translation.Locale),
                Is.EqualTo(new[] { "es", "fr" }));
            Assert.That(fixture.ViewModel.StatusText, Is.EqualTo("Updated catalog settings."));
            Assert.That(fixture.ViewModel.Entries.Single().LocaleStatuses, Does.Contain("fr:Missing"));
        });
    }

    [AvaloniaTest]
    public void CatalogSettings_ChangingTheSyntaxProfile_StalesApprovedText()
    {
        var catalog = CatalogBuilder.WithEntries(["es"], "a");
        CatalogBuilder.AddDraft(catalog, "a", "es", "Hola");
        var fixture = MainViewModelFixture.Open(catalog);
        fixture.Dialogs.SettingsResult = Settings("en", ["es"], MessageSyntaxProfile.Composite);

        fixture.ViewModel.ConfigureCatalogCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ViewModel.IsDirty);

        Assert.That(fixture.Translation("es").Status, Is.EqualTo("Stale"));
    }

    [AvaloniaTest]
    public void CatalogSettings_Unchanged_StaysClean()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Dialogs.SettingsResult = Settings("en", ["es"], MessageSyntaxProfile.Plain);

        fixture.ViewModel.ConfigureCatalogCommand.Execute(null);
        fixture.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.ViewModel.IsDirty, Is.False);
            Assert.That(fixture.ViewModel.StatusText, Is.EqualTo("Catalog settings unchanged."));
        });
    }

    [AvaloniaTest]
    public void CatalogSettings_Cancelled_ChangesNothing()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Dialogs.SettingsResult = null;

        fixture.ViewModel.ConfigureCatalogCommand.Execute(null);
        fixture.Pump();

        Assert.That(fixture.ViewModel.IsDirty, Is.False);
        Assert.That(fixture.ViewModel.Translations.Select(translation => translation.Locale), Is.EqualTo(new[] { "es" }));
    }

    [AvaloniaTest]
    public void CatalogSettings_Rejected_ReportsTheErrorAndKeepsTheCatalog()
    {
        var fixture = MainViewModelFixture.Open(CatalogBuilder.WithEntries(["es"], "a"));
        fixture.Dialogs.SettingsResult = Settings("en", [], MessageSyntaxProfile.Plain);

        fixture.ViewModel.ConfigureCatalogCommand.Execute(null);
        fixture.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.Messages, Has.Some.Contains("Catalog settings failed"));
            Assert.That(fixture.ViewModel.IsDirty, Is.False);
            Assert.That(fixture.Translation("es"), Is.Not.Null);
        });
    }

    private static ConfigureCatalogSettingsRequest Settings(
        string source,
        string[] required,
        MessageSyntaxProfile profile) =>
        new()
        {
            SourceLocale = source,
            RequiredLocales = required,
            DefaultSyntaxProfile = profile
        };
}
