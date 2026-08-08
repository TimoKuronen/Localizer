// Validates one message string and compares a translation against its source structure.
// Combines syntax parsing, placeholder parity, and configured entry constraints.
namespace Localizer.Core.Validation;

public static class EntryMessageValidator
{
    public static IReadOnlyList<ValidationDiagnostic> ValidateText(
        Catalogs.Catalog catalog,
        Catalogs.CatalogEntry entry,
        string text,
        Identity.Locale? locale)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        var syntaxProfile = catalog.GetEffectiveSyntaxProfile(entry);
        var parser = Syntax.MessageSyntaxParserFactory.GetParser(syntaxProfile);
        var parseResult = parser.Parse(text, entry.Key, locale);

        diagnostics.AddRange(parseResult.Diagnostics);
        diagnostics.AddRange(EntryConstraintValidator.Validate(text, entry.Constraints, entry.Key, locale));

        return diagnostics;
    }

    public static IReadOnlyList<ValidationDiagnostic> ValidateTranslationAgainstSource(
        Catalogs.Catalog catalog,
        Catalogs.CatalogEntry entry,
        Identity.Locale locale)
    {
        if (!entry.Translations.TryGetValue(locale, out var translation))
        {
            return [];
        }

        var diagnostics = new List<ValidationDiagnostic>();
        var syntaxProfile = catalog.GetEffectiveSyntaxProfile(entry);
        var parser = Syntax.MessageSyntaxParserFactory.GetParser(syntaxProfile);

        var sourceParse = parser.Parse(entry.SourceText, entry.Key, catalog.SourceLocale);
        diagnostics.AddRange(sourceParse.Diagnostics);

        var translationParse = parser.Parse(translation.Text, entry.Key, locale);
        diagnostics.AddRange(translationParse.Diagnostics);

        diagnostics.AddRange(EntryConstraintValidator.Validate(translation.Text, entry.Constraints, entry.Key, locale));

        if (sourceParse.Success
            && translationParse.Success
            && syntaxProfile == Syntax.MessageSyntaxProfile.Composite)
        {
            diagnostics.AddRange(PlaceholderStructureValidator.Compare(
                sourceParse.Message!,
                translationParse.Message!,
                entry.Key,
                locale));
        }

        return diagnostics;
    }
}
