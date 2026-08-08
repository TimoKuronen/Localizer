// Decides whether one translation is eligible for explicit human approval.
// Approval is blocked when any Error-level validation finding exists for that entry and locale.
namespace Localizer.Core.Validation;

public static class ApprovalValidationPolicy
{
    public static ValidationResult Evaluate(
        Catalogs.Catalog catalog,
        Catalogs.CatalogEntry entry,
        Identity.Locale locale) =>
        CatalogValidator.ValidateTranslation(catalog, entry, locale);
}
