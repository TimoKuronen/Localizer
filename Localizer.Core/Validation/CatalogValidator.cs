// Runs read-only validation across a whole catalog, one entry, or one translation.
// Collects syntax, constraint, and placeholder findings without changing any catalog data.
namespace Localizer.Core.Validation;

public static class CatalogValidator
{
    public static ValidationResult Validate(Catalogs.Catalog catalog)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var entry in catalog.Entries.Values.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal))
        {
            diagnostics.AddRange(ValidateEntry(catalog, entry).Diagnostics);
        }

        return ValidationResult.FromDiagnostics(diagnostics);
    }

    public static ValidationResult ValidateEntry(Catalogs.Catalog catalog, Catalogs.CatalogEntry entry)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        diagnostics.AddRange(EntryMessageValidator.ValidateText(
            catalog,
            entry,
            entry.SourceText,
            catalog.SourceLocale));

        foreach (var locale in entry.Translations.Keys.OrderBy(locale => locale.Value, StringComparer.Ordinal))
        {
            diagnostics.AddRange(ValidateTranslation(catalog, entry, locale).Diagnostics);
        }

        return ValidationResult.FromDiagnostics(diagnostics);
    }

    public static ValidationResult ValidateTranslation(
        Catalogs.Catalog catalog,
        Catalogs.CatalogEntry entry,
        Identity.Locale locale)
    {
        if (!entry.Translations.ContainsKey(locale))
        {
            return ValidationResult.Empty;
        }

        var diagnostics = EntryMessageValidator.ValidateTranslationAgainstSource(catalog, entry, locale);
        return ValidationResult.FromDiagnostics(diagnostics);
    }
}
