// Stable machine-readable codes for every validation finding in version 1.
// Codes stay fixed so tests, export reports, and future UI can rely on them.
namespace Localizer.Core.Validation;

public static class DiagnosticCodes
{
    public const string SyntaxCompositeUnbalancedBrace = "syntax.composite.unbalanced_brace";
    public const string SyntaxCompositeUnexpectedClosingBrace = "syntax.composite.unexpected_closing_brace";
    public const string SyntaxCompositeInvalidPlaceholder = "syntax.composite.invalid_placeholder";
    public const string SyntaxCompositeNamedPlaceholderUnsupported = "syntax.composite.named_placeholder_unsupported";

    public const string SyntaxPlaceholderMissing = "syntax.placeholder.missing";
    public const string SyntaxPlaceholderExtra = "syntax.placeholder.extra";
    public const string SyntaxPlaceholderIndexMismatch = "syntax.placeholder.index_mismatch";

    public const string ConstraintGraphemeExceeded = "constraint.grapheme.exceeded";
    public const string ConstraintUtf8BytesExceeded = "constraint.utf8_bytes.exceeded";
    public const string ConstraintLinesExceeded = "constraint.lines.exceeded";
    public const string ConstraintTermRequiredMissing = "constraint.term.required_missing";
    public const string ConstraintTermForbiddenPresent = "constraint.term.forbidden_present";

    public const string ExportTranslationMissing = "export.translation.missing";
    public const string ExportTranslationStale = "export.translation.stale";
    public const string ExportTranslationDraft = "export.translation.draft";
    public const string ExportTranslationInvalid = "export.translation.invalid";
}
