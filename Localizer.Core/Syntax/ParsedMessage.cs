// The structural result of parsing a message for a syntax profile.
// Exposes literal and placeholder segments plus the expanded placeholder index sequence.
namespace Localizer.Core.Syntax;

public sealed class ParsedMessage
{
    public ParsedMessage(IReadOnlyList<MessageSegment> segments, IReadOnlyList<int> placeholderIndices)
    {
        Segments = segments;
        PlaceholderIndices = placeholderIndices;
    }

    public IReadOnlyList<MessageSegment> Segments { get; }

    public IReadOnlyList<int> PlaceholderIndices { get; }
}
