// Parses plain text as one literal segment with no placeholder slots.
// Used when an entry uses the plain syntax profile.
namespace Localizer.Core.Syntax;

public sealed class PlainMessageParser : IMessageSyntaxParser
{
    public MessageParseResult Parse(
        string text,
        Identity.EntryKey? entryKey = null,
        Identity.Locale? locale = null)
    {
        var segments = new MessageSegment[] { new LiteralSegment(text) };
        var message = new ParsedMessage(segments, []);
        return MessageParseResult.Succeeded(message);
    }
}
