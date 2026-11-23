using Localizer.Core.Catalogs;
using Localizer.Core.Fingerprints;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;

namespace Localizer.Core.Tests;

public sealed class FingerprintCanonicalizationTests
{
    private static readonly Locale English = Locale.Create("en");

    [Test]
    public void BuildCanonicalInput_ProducesFixedLayout()
    {
        var canonical = Canonical(
            "Start",
            notes: "Main menu",
            context: new Dictionary<string, string> { ["screen"] = "menu" },
            constraints: new EntryConstraints { MaxLines = 1, RequiredTerms = ["Go"] });

        Assert.That(
            canonical,
            Is.EqualTo(
                "fingerprintVersion=1\n"
                + "sourceLocale=en\n"
                + "syntaxProfile=plain\n"
                + "[sourceText]\nStart\n[/sourceText]\n"
                + "[developerNotes]\nMain menu\n[/developerNotes]\n"
                + "[context]\nscreen=menu\n[/context]\n"
                + "[constraints]\nmaxLines=1\nrequiredTerm=Go\n[/constraints]\n"));
    }

    [Test]
    public void BuildCanonicalInput_TreatsAllNewlineStylesAsEqual()
    {
        var unix = Canonical("a\nb", notes: "n\nm");
        var windows = Canonical("a\r\nb", notes: "n\r\nm");
        var classicMac = Canonical("a\rb", notes: "n\rm");

        Assert.That(windows, Is.EqualTo(unix));
        Assert.That(classicMac, Is.EqualTo(unix));
    }

    [Test]
    public void BuildCanonicalInput_IgnoresContextInsertionOrder()
    {
        var first = Canonical(
            "Start",
            context: new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });
        var second = Canonical(
            "Start",
            context: new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void BuildCanonicalInput_IgnoresTermOrder()
    {
        var first = Canonical(
            "Start",
            constraints: new EntryConstraints { RequiredTerms = ["b", "a"], ForbiddenTerms = ["y", "x"] });
        var second = Canonical(
            "Start",
            constraints: new EntryConstraints { RequiredTerms = ["a", "b"], ForbiddenTerms = ["x", "y"] });

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void BuildCanonicalInput_TreatsMissingNotesAsEmpty()
    {
        Assert.That(Canonical("Start", notes: null), Is.EqualTo(Canonical("Start", notes: string.Empty)));
    }

    [Test]
    public void BuildCanonicalInput_DistinguishesRequiredFromForbiddenTerms()
    {
        var required = Canonical("Start", constraints: new EntryConstraints { RequiredTerms = ["x"] });
        var forbidden = Canonical("Start", constraints: new EntryConstraints { ForbiddenTerms = ["x"] });

        Assert.That(required, Is.Not.EqualTo(forbidden));
    }

    [Test]
    public void BuildCanonicalInput_IsSensitiveToEachInput()
    {
        var baseline = Canonical("Start");

        Assert.Multiple(() =>
        {
            Assert.That(Canonical("Stop"), Is.Not.EqualTo(baseline));
            Assert.That(Canonical("Start", notes: "n"), Is.Not.EqualTo(baseline));
            Assert.That(
                Canonical("Start", context: new Dictionary<string, string> { ["k"] = "v" }),
                Is.Not.EqualTo(baseline));
            Assert.That(
                Canonical("Start", constraints: new EntryConstraints { MaxGraphemes = 5 }),
                Is.Not.EqualTo(baseline));
            Assert.That(
                Canonical("Start", constraints: new EntryConstraints { MaxUtf8Bytes = 5 }),
                Is.Not.EqualTo(baseline));
            Assert.That(
                Canonical("Start", sourceLocale: Locale.Create("fi")),
                Is.Not.EqualTo(baseline));
            Assert.That(
                Canonical("Start", profile: MessageSyntaxProfile.Composite),
                Is.Not.EqualTo(baseline));
        });
    }

    [Test]
    public void BuildCanonicalInput_KeepsSectionBoundariesUnambiguous()
    {
        var textWithMarker = Canonical("a\n[/sourceText]\n[developerNotes]\nb");
        var notesInstead = Canonical("a", notes: "b");

        Assert.That(textWithMarker, Is.Not.EqualTo(notesInstead));
    }

    private static string Canonical(
        string text,
        string? notes = null,
        IReadOnlyDictionary<string, string>? context = null,
        EntryConstraints? constraints = null,
        Locale? sourceLocale = null,
        MessageSyntaxProfile profile = MessageSyntaxProfile.Plain) =>
        FingerprintCanonicalization.BuildCanonicalInput(
            sourceLocale ?? English,
            text,
            profile,
            notes,
            new TranslationContext { Values = context ?? new Dictionary<string, string>() },
            constraints ?? new EntryConstraints());
}
