// One validation finding with a stable code, message, and optional entry, locale, and location.
// Findings are read-only reports; they never change catalog content by themselves.
namespace Localizer.Core.Validation;

public sealed record ValidationDiagnostic(
    string Code,
    ValidationSeverity Severity,
    string Message,
    Identity.EntryKey? EntryKey = null,
    Identity.Locale? Locale = null,
    ValidationLocation? Location = null);
