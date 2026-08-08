// Wraps a parse attempt with success status and any syntax diagnostics found along the way.
// A failed parse still returns diagnostics but does not expose a usable parsed message.
namespace Localizer.Core.Syntax;

public sealed class MessageParseResult
{
    private MessageParseResult(
        bool success,
        ParsedMessage? message,
        IReadOnlyList<Validation.ValidationDiagnostic> diagnostics)
    {
        Success = success;
        Message = message;
        Diagnostics = diagnostics;
    }

    public bool Success { get; }

    public ParsedMessage? Message { get; }

    public IReadOnlyList<Validation.ValidationDiagnostic> Diagnostics { get; }

    public static MessageParseResult Succeeded(ParsedMessage message) =>
        new(true, message, []);

    public static MessageParseResult Failed(IReadOnlyList<Validation.ValidationDiagnostic> diagnostics) =>
        new(false, null, diagnostics);
}
