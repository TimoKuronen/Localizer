// A sorted collection of validation findings returned from a read-only validation pass.
// Callers use HasBlockingErrors to decide whether approval or export may proceed.
namespace Localizer.Core.Validation;

public sealed class ValidationResult
{
    private ValidationResult(IReadOnlyList<ValidationDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<ValidationDiagnostic> Diagnostics { get; }

    public bool HasBlockingErrors =>
        Diagnostics.Any(diagnostic => diagnostic.Severity == ValidationSeverity.Error);

    public static ValidationResult Empty { get; } = new([]);

    public static ValidationResult FromDiagnostics(IEnumerable<ValidationDiagnostic> diagnostics) =>
        new(ValidationOrdering.Sort(diagnostics));
}
