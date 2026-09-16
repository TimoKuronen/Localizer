using Localizer.Application.Drafting;
using Localizer.Infrastructure.Drafting;

namespace Localizer.Infrastructure.Tests.Drafting;

public sealed class OllamaTranslationDraftProviderTests
{
    [Test]
    public void ResolveModelName_FallsBackToSoleInstalledModel()
    {
        var resolved = OllamaTranslationDraftProvider.ResolveModelName(
            "llama3.2",
            ["qwen3.5:4b"]);

        Assert.That(resolved, Is.EqualTo("qwen3.5:4b"));
    }

    [Test]
    public void ResolveModelName_PrefersExactInstalledMatch()
    {
        var resolved = OllamaTranslationDraftProvider.ResolveModelName(
            "llama3.2",
            ["qwen3.5:4b", "llama3.2:latest"]);

        Assert.That(resolved, Is.EqualTo("llama3.2:latest"));
    }

    [Test]
    public void ResolveModelName_FailsWhenPreferredMissingAmongMany()
    {
        Assert.That(
            () => OllamaTranslationDraftProvider.ResolveModelName(
                "llama3.2",
                ["qwen3.5:4b", "mistral:latest"]),
            Throws.InvalidOperationException.With.Message.Contains("LOCALIZER_OLLAMA_MODEL"));
    }

    [Test]
    public void BuildUserPrompt_IncludesNotesContextConstraintsAndSyntax()
    {
        var prompt = OllamaTranslationDraftProvider.BuildUserPrompt(new TranslationDraftItemRequest
        {
            Key = "ui.coins",
            SourceLocale = "en",
            TargetLocale = "es",
            SourceText = "You have {0} coins",
            DeveloperNotes = "Keep the number placeholder.",
            Context = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["screen"] = "hud",
                ["tone"] = "neutral"
            },
            Constraints = new DraftConstraintHints
            {
                MaxGraphemes = 40,
                RequiredTerms = ["Steam"],
                ForbiddenTerms = ["beta"]
            },
            SyntaxProfile = "composite"
        });

        Assert.That(prompt, Does.Contain("Translate from en to es."));
        Assert.That(prompt, Does.Contain("Key: ui.coins"));
        Assert.That(prompt, Does.Contain("Syntax profile: composite"));
        Assert.That(prompt, Does.Contain("You have {0} coins"));
        Assert.That(prompt, Does.Contain("Developer notes:"));
        Assert.That(prompt, Does.Contain("Keep the number placeholder."));
        Assert.That(prompt, Does.Contain("- screen: hud"));
        Assert.That(prompt, Does.Contain("- tone: neutral"));
        Assert.That(prompt, Does.Contain("- Max graphemes: 40"));
        Assert.That(prompt, Does.Contain("- Required terms: Steam"));
        Assert.That(prompt, Does.Contain("- Forbidden terms: beta"));
    }

    [Test]
    public void ParseDraftContent_AcceptsMatchingJson()
    {
        var item = CreateItem();
        var result = OllamaTranslationDraftProvider.ParseDraftContent(
            item,
            """{"key":"ui.start","locale":"es","text":"Empezar"}""");

        Assert.That(result.Key, Is.EqualTo("ui.start"));
        Assert.That(result.TargetLocale, Is.EqualTo("es"));
        Assert.That(result.Text, Is.EqualTo("Empezar"));
    }

    [Test]
    public void ParseDraftContent_UsesRequestedIdentityWhenKeyLocaleOmitted()
    {
        var item = CreateItem();
        var result = OllamaTranslationDraftProvider.ParseDraftContent(item, """{"text":"Empezar"}""");

        Assert.That(result.Key, Is.EqualTo("ui.start"));
        Assert.That(result.TargetLocale, Is.EqualTo("es"));
        Assert.That(result.Text, Is.EqualTo("Empezar"));
    }

    [Test]
    public void ParseDraftContent_RejectsMismatchedKey()
    {
        var item = CreateItem();

        Assert.That(
            () => OllamaTranslationDraftProvider.ParseDraftContent(
                item,
                """{"key":"other","locale":"es","text":"Empezar"}"""),
            Throws.InvalidOperationException);
    }

    [Test]
    public void ParseDraftContent_ExtractsJsonFromSurroundingText()
    {
        var item = CreateItem();
        var result = OllamaTranslationDraftProvider.ParseDraftContent(
            item,
            "Here you go:\n{\"text\":\"Empezar\"}\n");

        Assert.That(result.Text, Is.EqualTo("Empezar"));
    }

    private static TranslationDraftItemRequest CreateItem() =>
        new()
        {
            Key = "ui.start",
            SourceLocale = "en",
            TargetLocale = "es",
            SourceText = "Start",
            SyntaxProfile = "plain"
        };
}
