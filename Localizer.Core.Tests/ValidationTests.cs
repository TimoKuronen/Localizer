using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;
using Localizer.Core.Validation;

namespace Localizer.Core.Tests;

public sealed class CompositeMessageParserTests
{
    private static CompositeMessageParser CreateParser() => new();

    [Test]
    public void Parse_AcceptsIndexedPlaceholder()
    {
        var result = CreateParser().Parse("Score: {0}");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Message!.PlaceholderIndices, Is.EqualTo(new[] { 0 }));
    }

    [Test]
    public void Parse_AcceptsFormatterOptionsWithoutTreatingThemAsSeparatePlaceholders()
    {
        var result = CreateParser().Parse("Value: {0:N2}");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Message!.PlaceholderIndices, Is.EqualTo(new[] { 0 }));
    }

    [Test]
    public void Parse_AcceptsEscapedBraces()
    {
        var result = CreateParser().Parse("Use {{name}} here, not {0}.");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Message!.PlaceholderIndices, Is.EqualTo(new[] { 0 }));
        var literalText = string.Concat(result.Message.Segments.OfType<LiteralSegment>().Select(segment => segment.Text));
        Assert.That(literalText, Is.EqualTo("Use {name} here, not ."));
    }

    [Test]
    public void Parse_RejectsUnbalancedOpeningBrace()
    {
        var result = CreateParser().Parse("Hello {0");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(DiagnosticCodes.SyntaxCompositeUnbalancedBrace));
    }

    [Test]
    public void Parse_RejectsUnexpectedClosingBrace()
    {
        var result = CreateParser().Parse("Hello } there");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(DiagnosticCodes.SyntaxCompositeUnexpectedClosingBrace));
    }

    [Test]
    public void Parse_RejectsNamedPlaceholder()
    {
        var result = CreateParser().Parse("Hello {name}");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(DiagnosticCodes.SyntaxCompositeNamedPlaceholderUnsupported));
    }

    [Test]
    public void Parse_RejectsEmptyPlaceholder()
    {
        var result = CreateParser().Parse("Hello {}");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(DiagnosticCodes.SyntaxCompositeInvalidPlaceholder));
    }
}

public sealed class PlaceholderStructureValidatorTests
{
    private static ParsedMessage ParseComposite(string text)
    {
        var result = new CompositeMessageParser().Parse(text);
        Assert.That(result.Success, Is.True);
        return result.Message!;
    }

    [Test]
    public void Compare_AcceptsMatchingPlaceholderSequence()
    {
        var source = ParseComposite("Score: {0}");
        var translation = ParseComposite("Pisteet: {0}");
        var entryKey = EntryKey.Create("score_label");
        var locale = Locale.Create("fi");

        var diagnostics = PlaceholderStructureValidator.Compare(source, translation, entryKey, locale);

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Compare_RejectsRenamedPlaceholderIndex()
    {
        var source = ParseComposite("Items: {0}");
        var translation = ParseComposite("Kohteet: {1}");
        var entryKey = EntryKey.Create("items_label");
        var locale = Locale.Create("fi");

        var diagnostics = PlaceholderStructureValidator.Compare(source, translation, entryKey, locale);

        Assert.That(diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(DiagnosticCodes.SyntaxPlaceholderIndexMismatch));
    }

    [Test]
    public void Compare_RejectsMissingPlaceholder()
    {
        var source = ParseComposite("{0} and {1}");
        var translation = ParseComposite("vain {0}");
        var entryKey = EntryKey.Create("pair_label");
        var locale = Locale.Create("fi");

        var diagnostics = PlaceholderStructureValidator.Compare(source, translation, entryKey, locale);

        Assert.That(diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(DiagnosticCodes.SyntaxPlaceholderMissing));
    }

    [Test]
    public void Compare_RejectsExtraPlaceholder()
    {
        var source = ParseComposite("Only {0}");
        var translation = ParseComposite("{0} plus {1}");
        var entryKey = EntryKey.Create("extra_label");
        var locale = Locale.Create("fi");

        var diagnostics = PlaceholderStructureValidator.Compare(source, translation, entryKey, locale);

        Assert.That(diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(DiagnosticCodes.SyntaxPlaceholderExtra));
    }

    [Test]
    public void Compare_RejectsMultiplicityMismatch()
    {
        var source = ParseComposite("{0} and {0}");
        var translation = ParseComposite("vain {0}");
        var entryKey = EntryKey.Create("repeat_label");
        var locale = Locale.Create("fi");

        var diagnostics = PlaceholderStructureValidator.Compare(source, translation, entryKey, locale);

        Assert.That(diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(DiagnosticCodes.SyntaxPlaceholderMissing));
    }
}

public sealed class EntryConstraintValidatorTests
{
    private static readonly EntryKey EntryKey = Identity.EntryKey.Create("button_label");
    private static readonly Locale Locale = Identity.Locale.Create("fi");

    [Test]
    public void Validate_CountsCombiningCharactersAsOneGrapheme()
    {
        var text = "e\u0301";
        var constraints = new EntryConstraints { MaxGraphemes = 1 };

        var diagnostics = EntryConstraintValidator.Validate(text, constraints, EntryKey, Locale);

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Validate_RejectsTextThatExceedsGraphemeLimit()
    {
        var constraints = new EntryConstraints { MaxGraphemes = 3 };

        var diagnostics = EntryConstraintValidator.Validate("ABCD", constraints, EntryKey, Locale);

        Assert.That(diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ConstraintGraphemeExceeded));
    }

    [Test]
    public void Validate_RejectsTextThatExceedsUtf8ByteLimit()
    {
        var constraints = new EntryConstraints { MaxUtf8Bytes = 3 };

        var diagnostics = EntryConstraintValidator.Validate("Aloita", constraints, EntryKey, Locale);

        Assert.That(diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ConstraintUtf8BytesExceeded));
    }

    [Test]
    public void Validate_RejectsTextThatExceedsLineLimit()
    {
        var constraints = new EntryConstraints { MaxLines = 1 };

        var diagnostics = EntryConstraintValidator.Validate("Line one\nLine two", constraints, EntryKey, Locale);

        Assert.That(diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ConstraintLinesExceeded));
    }

    [Test]
    public void Validate_RejectsMissingRequiredTerm()
    {
        var constraints = new EntryConstraints { RequiredTerms = ["Steam"] };

        var diagnostics = EntryConstraintValidator.Validate("Buy now", constraints, EntryKey, Locale);

        Assert.That(diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ConstraintTermRequiredMissing));
    }

    [Test]
    public void Validate_RejectsForbiddenTerm()
    {
        var constraints = new EntryConstraints { ForbiddenTerms = ["beta"] };

        var diagnostics = EntryConstraintValidator.Validate("beta access", constraints, EntryKey, Locale);

        Assert.That(diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ConstraintTermForbiddenPresent));
    }

    [Test]
    public void Validate_ForbiddenTermMatchingIsCaseSensitive()
    {
        var constraints = new EntryConstraints { ForbiddenTerms = ["beta"] };

        var diagnostics = EntryConstraintValidator.Validate("Beta access", constraints, EntryKey, Locale);

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public void Validate_RequiredTermMatchingIsCaseSensitive()
    {
        var constraints = new EntryConstraints { RequiredTerms = ["Steam"] };

        var diagnostics = EntryConstraintValidator.Validate("Available on steam", constraints, EntryKey, Locale);

        Assert.That(diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.ConstraintTermRequiredMissing));
    }
}
