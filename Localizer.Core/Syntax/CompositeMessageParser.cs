// Parses indexed composite placeholders like {0} and treats {{ and }} as literal braces.
// Version 1 compares placeholder indices only, not formatter options inside the braces.
namespace Localizer.Core.Syntax;

public sealed class CompositeMessageParser : IMessageSyntaxParser
{
    public MessageParseResult Parse(
        string text,
        Identity.EntryKey? entryKey = null,
        Identity.Locale? locale = null)
    {
        var segments = new List<MessageSegment>();
        var placeholderIndices = new List<int>();
        var diagnostics = new List<Validation.ValidationDiagnostic>();
        var literalBuilder = new System.Text.StringBuilder();

        void FlushLiteral()
        {
            if (literalBuilder.Length == 0)
            {
                return;
            }

            segments.Add(new LiteralSegment(literalBuilder.ToString()));
            literalBuilder.Clear();
        }

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '{')
            {
                if (index + 1 < text.Length && text[index + 1] == '{')
                {
                    literalBuilder.Append('{');
                    index++;
                    continue;
                }

                FlushLiteral();
                var placeholderStart = index;
                var closingBraceIndex = text.IndexOf('}', index + 1);
                if (closingBraceIndex < 0)
                {
                    diagnostics.Add(CreateDiagnostic(
                        Validation.DiagnosticCodes.SyntaxCompositeUnbalancedBrace,
                        "Composite text contains an opening brace without a matching closing brace.",
                        entryKey,
                        locale,
                        new Validation.ValidationLocation(placeholderStart, text.Length - placeholderStart)));
                    break;
                }

                var inner = text.Substring(index + 1, closingBraceIndex - index - 1);
                if (!TryParsePlaceholder(inner, out var placeholderIndex, out var rawFormat, out var placeholderDiagnostic))
                {
                    diagnostics.Add(placeholderDiagnostic! with
                    {
                        Location = new Validation.ValidationLocation(
                            placeholderStart,
                            closingBraceIndex - placeholderStart + 1)
                    });
                }
                else
                {
                    segments.Add(new PlaceholderSegment(placeholderIndex, rawFormat));
                    placeholderIndices.Add(placeholderIndex);
                }

                index = closingBraceIndex;
                continue;
            }

            if (character == '}')
            {
                if (index + 1 < text.Length && text[index + 1] == '}')
                {
                    literalBuilder.Append('}');
                    index++;
                    continue;
                }

                diagnostics.Add(CreateDiagnostic(
                    Validation.DiagnosticCodes.SyntaxCompositeUnexpectedClosingBrace,
                    "Composite text contains a closing brace that is not part of a placeholder or escaped pair.",
                    entryKey,
                    locale,
                    new Validation.ValidationLocation(index, 1)));
                continue;
            }

            literalBuilder.Append(character);
        }

        FlushLiteral();

        if (diagnostics.Count > 0)
        {
            return MessageParseResult.Failed(diagnostics);
        }

        return MessageParseResult.Succeeded(new ParsedMessage(segments, placeholderIndices));
    }

    private static bool TryParsePlaceholder(
        string inner,
        out int placeholderIndex,
        out string rawFormat,
        out Validation.ValidationDiagnostic? diagnostic)
    {
        placeholderIndex = default;
        rawFormat = string.Empty;
        diagnostic = null;

        if (inner.Length == 0)
        {
            diagnostic = CreateDiagnostic(
                Validation.DiagnosticCodes.SyntaxCompositeInvalidPlaceholder,
                "Composite placeholders cannot be empty.");
            return false;
        }

        var indexLength = 0;
        while (indexLength < inner.Length && char.IsDigit(inner[indexLength]))
        {
            indexLength++;
        }

        if (indexLength == 0)
        {
            diagnostic = CreateDiagnostic(
                Validation.DiagnosticCodes.SyntaxCompositeNamedPlaceholderUnsupported,
                "Composite placeholders must use zero-based numeric indices in version 1.");
            return false;
        }

        if (!int.TryParse(inner[..indexLength], out placeholderIndex))
        {
            diagnostic = CreateDiagnostic(
                Validation.DiagnosticCodes.SyntaxCompositeInvalidPlaceholder,
                "Composite placeholder index is out of range.");
            return false;
        }

        if (indexLength < inner.Length)
        {
            if (inner[indexLength] != ':')
            {
                diagnostic = CreateDiagnostic(
                    Validation.DiagnosticCodes.SyntaxCompositeInvalidPlaceholder,
                    "Composite placeholder contains unsupported syntax after the index.");
                return false;
            }

            rawFormat = inner[indexLength..];
        }

        return true;
    }

    private static Validation.ValidationDiagnostic CreateDiagnostic(
        string code,
        string message,
        Identity.EntryKey? entryKey = null,
        Identity.Locale? locale = null,
        Validation.ValidationLocation? location = null) =>
        new(
            code,
            Validation.ValidationSeverity.Error,
            message,
            entryKey,
            locale,
            location);
}
