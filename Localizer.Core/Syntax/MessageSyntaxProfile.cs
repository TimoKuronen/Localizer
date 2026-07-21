namespace Localizer.Core.Syntax;

public enum MessageSyntaxProfile
{
    Plain,
    Composite
}

public static class MessageSyntaxProfileNames
{
    public const string Plain = "plain";
    public const string Composite = "composite";

    public static string ToName(MessageSyntaxProfile profile) =>
        profile switch
        {
            MessageSyntaxProfile.Plain => Plain,
            MessageSyntaxProfile.Composite => Composite,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };

    public static MessageSyntaxProfile FromName(string value) =>
        value switch
        {
            Plain => MessageSyntaxProfile.Plain,
            Composite => MessageSyntaxProfile.Composite,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported syntax profile.")
        };
}
