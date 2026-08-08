// Decides whether a catalog is ready for production export across all required locales.
// Blocks export when work is missing, stale, unapproved, or has blocking validation errors.
namespace Localizer.Core.Validation;

public static class ExportValidationPolicy
{
    public static ValidationResult Evaluate(Catalogs.Catalog catalog)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var entry in catalog.Entries.Values.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal))
        {
            foreach (var locale in catalog.RequiredLocales)
            {
                var effectiveStatus = catalog.GetEffectiveStatus(entry, locale);

                switch (effectiveStatus)
                {
                    case Lifecycle.TranslationEffectiveStatus.Missing:
                        diagnostics.Add(new ValidationDiagnostic(
                            DiagnosticCodes.ExportTranslationMissing,
                            ValidationSeverity.Error,
                            $"Required translation for locale '{locale.Value}' is missing.",
                            entry.Key,
                            locale));
                        continue;
                    case Lifecycle.TranslationEffectiveStatus.Stale:
                        diagnostics.Add(new ValidationDiagnostic(
                            DiagnosticCodes.ExportTranslationStale,
                            ValidationSeverity.Error,
                            $"Translation for locale '{locale.Value}' is stale and must be revised.",
                            entry.Key,
                            locale));
                        continue;
                    case Lifecycle.TranslationEffectiveStatus.Draft:
                        diagnostics.Add(new ValidationDiagnostic(
                            DiagnosticCodes.ExportTranslationDraft,
                            ValidationSeverity.Error,
                            $"Translation for locale '{locale.Value}' is still a draft.",
                            entry.Key,
                            locale));
                        continue;
                }

                var translationValidation = CatalogValidator.ValidateTranslation(catalog, entry, locale);
                if (translationValidation.HasBlockingErrors)
                {
                    diagnostics.Add(new ValidationDiagnostic(
                        DiagnosticCodes.ExportTranslationInvalid,
                        ValidationSeverity.Error,
                        $"Translation for locale '{locale.Value}' has blocking validation errors.",
                        entry.Key,
                        locale));
                    diagnostics.AddRange(translationValidation.Diagnostics);
                }
            }
        }

        return ValidationResult.FromDiagnostics(diagnostics);
    }
}
