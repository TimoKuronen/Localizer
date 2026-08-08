// Compares placeholder index sequences between source and translation parsed messages.
// Ensures both strings use the same numbered slots in the same order and count.
namespace Localizer.Core.Validation;

public static class PlaceholderStructureValidator
{
    public static IReadOnlyList<ValidationDiagnostic> Compare(
        Syntax.ParsedMessage source,
        Syntax.ParsedMessage translation,
        Identity.EntryKey entryKey,
        Identity.Locale locale)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        var sourceIndices = source.PlaceholderIndices;
        var translationIndices = translation.PlaceholderIndices;
        var sharedLength = Math.Min(sourceIndices.Count, translationIndices.Count);

        for (var index = 0; index < sharedLength; index++)
        {
            if (sourceIndices[index] == translationIndices[index])
            {
                continue;
            }

            diagnostics.Add(new ValidationDiagnostic(
                DiagnosticCodes.SyntaxPlaceholderIndexMismatch,
                ValidationSeverity.Error,
                $"Translation placeholder at position {index} must use index {{{sourceIndices[index]}}}, but found {{{translationIndices[index]}}}.",
                entryKey,
                locale));
        }

        if (translationIndices.Count < sourceIndices.Count)
        {
            for (var index = translationIndices.Count; index < sourceIndices.Count; index++)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    DiagnosticCodes.SyntaxPlaceholderMissing,
                    ValidationSeverity.Error,
                    $"Translation is missing placeholder index {{{sourceIndices[index]}}}.",
                    entryKey,
                    locale));
            }
        }

        if (translationIndices.Count > sourceIndices.Count)
        {
            for (var index = sourceIndices.Count; index < translationIndices.Count; index++)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    DiagnosticCodes.SyntaxPlaceholderExtra,
                    ValidationSeverity.Error,
                    $"Translation contains unexpected placeholder index {{{translationIndices[index]}}}.",
                    entryKey,
                    locale));
            }
        }

        return diagnostics;
    }
}
