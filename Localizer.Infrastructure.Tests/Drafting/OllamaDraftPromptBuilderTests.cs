using Localizer.Application.Drafting;
using Localizer.Infrastructure.Drafting;

namespace Localizer.Infrastructure.Tests.Drafting;

public sealed class OllamaDraftPromptBuilderTests
{
    [Test]
    public void BuildUserPrompt_IncludesNotesContextConstraintsAndSyntax()
    {
        var prompt = OllamaDraftPromptBuilder.BuildUserPrompt(new TranslationDraftItemRequest
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
    public void BuildSystemPrompt_OmitsGuidanceSectionsWhenUnset()
    {
        var prompt = OllamaDraftPromptBuilder.BuildSystemPrompt(null, "  ");

        Assert.That(prompt, Does.Not.Contain("About the product"));
        Assert.That(prompt, Does.Not.Contain("Glossary"));
        Assert.That(prompt, Does.Contain("{0}"));
    }

    [Test]
    public void BuildUserPrompt_MarksSourceAndFlagsNotesAsReferenceOnly()
    {
        var prompt = OllamaDraftPromptBuilder.BuildUserPrompt(CreateItem() with
        {
            DeveloperNotes = "Toolbar button."
        });

        Assert.That(prompt, Does.Contain("<<<\nStart\n>>>").Or.Contain("<<<\r\nStart\r\n>>>"));
        Assert.That(prompt, Does.Contain("reference only"));
    }

    private static TranslationDraftItemRequest CreateItem(string key = "ui.start") =>
        new()
        {
            Key = key,
            SourceLocale = "en",
            TargetLocale = "es",
            SourceText = "Start",
            SyntaxProfile = "plain"
        };

}
