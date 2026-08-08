// How serious a validation finding is for reviewers and export checks.
// Errors block approval and export; warnings are reported but do not block in version 1.
namespace Localizer.Core.Validation;

public enum ValidationSeverity
{
    Error,
    Warning
}
