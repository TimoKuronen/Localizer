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
        diagnostics.AddRange(ValidateSourceShape(catalog, entry));

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

    // Advisory only: glue strings such as "{0} {1}" have nothing to translate, which confuses reviewers.
    // Kept out of translation validation so it never affects approval or export.
    private static IEnumerable<ValidationDiagnostic> ValidateSourceShape(
        Catalogs.Catalog catalog,
        Catalogs.CatalogEntry entry)
    {
        if (catalog.GetEffectiveSyntaxProfile(entry) != Syntax.MessageSyntaxProfile.Composite)
        {
            yield break;
        }

        var parser = Syntax.MessageSyntaxParserFactory.GetParser(Syntax.MessageSyntaxProfile.Composite);
        var parse = parser.Parse(entry.SourceText, entry.Key, catalog.SourceLocale);
        if (!parse.Success || !IsPlaceholderOnly(parse.Message!))
        {
            yield break;
        }

        yield return new ValidationDiagnostic(
            DiagnosticCodes.SourcePlaceholderOnly,
            ValidationSeverity.Warning,
            "Source contains only placeholders. Approve translations with the same placeholders, or leave them empty.",
            entry.Key,
            catalog.SourceLocale);
    }

    private static bool IsPlaceholderOnly(Syntax.ParsedMessage message) =>
        message.Segments.Any(segment => segment is Syntax.PlaceholderSegment)
        && message.Segments.All(segment =>
            segment is Syntax.PlaceholderSegment
            || segment is Syntax.LiteralSegment literal && string.IsNullOrWhiteSpace(literal.Text));
}
