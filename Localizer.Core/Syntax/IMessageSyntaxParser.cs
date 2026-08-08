// Contract for parsing source or translation text according to a message syntax profile.
// Parsers are read-only and report structural problems as validation diagnostics.
namespace Localizer.Core.Syntax;

public interface IMessageSyntaxParser
{
    MessageParseResult Parse(
        string text,
        Identity.EntryKey? entryKey = null,
        Identity.Locale? locale = null);
}
