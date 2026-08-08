// Parsed pieces of a message string: either literal text or an indexed placeholder slot.
// Validators compare these segments without requiring translated words to stay identical.
namespace Localizer.Core.Syntax;

public abstract record MessageSegment;

public sealed record LiteralSegment(string Text) : MessageSegment;

public sealed record PlaceholderSegment(int Index, string RawFormat) : MessageSegment;
