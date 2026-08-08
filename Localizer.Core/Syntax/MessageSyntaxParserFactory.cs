// Returns the parser that matches a catalog or entry syntax profile name.
// Keeps profile selection in one place so validators stay profile-agnostic.
namespace Localizer.Core.Syntax;

public static class MessageSyntaxParserFactory
{
    private static readonly PlainMessageParser PlainParser = new();
    private static readonly CompositeMessageParser CompositeParser = new();

    public static IMessageSyntaxParser GetParser(MessageSyntaxProfile profile) =>
        profile switch
        {
            MessageSyntaxProfile.Plain => PlainParser,
            MessageSyntaxProfile.Composite => CompositeParser,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };
}
