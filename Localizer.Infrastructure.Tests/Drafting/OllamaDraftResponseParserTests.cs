using System.Text.Json;
using Localizer.Application.Drafting;
using Localizer.Infrastructure.Drafting;

namespace Localizer.Infrastructure.Tests.Drafting;

public sealed class OllamaDraftResponseParserTests
{
    [TestCase("Aloita\nDeveloper notes: Toolbar button.")]
    [TestCase("Konteksti:\nContext: toolbar")]
    [TestCase("Aloita\nLisärivi")]
    public void ParseDraftContent_RejectsEchoedPromptText(string text)
    {
        var json = JsonSerializer.Serialize(new { text });

        Assert.That(
            () => OllamaDraftResponseParser.ParseDraftContent(CreateItem(), json),
            Throws.InvalidOperationException);
    }

    [Test]
    public void ParseDraftContent_RejectsTextFarLongerThanSource()
    {
        var json = JsonSerializer.Serialize(new { text = new string('a', 200) });

        Assert.That(
            () => OllamaDraftResponseParser.ParseDraftContent(CreateItem(), json),
            Throws.InvalidOperationException.With.Message.Contains("longer"));
    }

    [Test]
    public void ParseDraftContent_IgnoresTrailingNewlineWhenCountingLines()
    {
        var json = JsonSerializer.Serialize(new { text = "Aloita\n" });

        var result = OllamaDraftResponseParser.ParseDraftContent(CreateItem(), json);

        Assert.That(result.Text, Is.EqualTo("Aloita\n"));
    }

    [Test]
    public void ParseDraftContent_AcceptsMultiLineTextMatchingSourceLines()
    {
        var item = CreateItem() with { SourceText = "First line.\nSecond line." };
        var json = JsonSerializer.Serialize(new { text = "Ensimmäinen rivi.\nToinen rivi." });

        var result = OllamaDraftResponseParser.ParseDraftContent(item, json);

        Assert.That(result.Text, Is.EqualTo("Ensimmäinen rivi.\nToinen rivi."));
    }

    [Test]
    public void ParseDraftContent_AcceptsMatchingJson()
    {
        var item = CreateItem();
        var result = OllamaDraftResponseParser.ParseDraftContent(
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
        var result = OllamaDraftResponseParser.ParseDraftContent(item, """{"text":"Empezar"}""");

        Assert.That(result.Key, Is.EqualTo("ui.start"));
        Assert.That(result.TargetLocale, Is.EqualTo("es"));
        Assert.That(result.Text, Is.EqualTo("Empezar"));
    }

    [Test]
    public void ParseDraftContent_RejectsMismatchedKey()
    {
        var item = CreateItem();

        Assert.That(
            () => OllamaDraftResponseParser.ParseDraftContent(
                item,
                """{"key":"other","locale":"es","text":"Empezar"}"""),
            Throws.InvalidOperationException);
    }

    [Test]
    public void ParseDraftContent_ExtractsJsonFromSurroundingText()
    {
        var item = CreateItem();
        var result = OllamaDraftResponseParser.ParseDraftContent(
            item,
            "Here you go:\n{\"text\":\"Empezar\"}\n");

        Assert.That(result.Text, Is.EqualTo("Empezar"));
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
