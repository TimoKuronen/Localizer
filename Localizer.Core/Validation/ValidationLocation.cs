// Points to the part of a string where a validation finding applies.
// Uses a character offset and length so diagnostics can highlight the exact span.
namespace Localizer.Core.Validation;

public sealed record ValidationLocation(int StartOffset, int Length);
