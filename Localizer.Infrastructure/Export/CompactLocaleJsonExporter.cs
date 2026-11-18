using Localizer.Application.Export;
using Localizer.Core.Catalogs;
using Localizer.Core.Lifecycle;

namespace Localizer.Infrastructure.Export;

public sealed class CompactLocaleJsonExporter : ICatalogExporter
{
    public Task<IReadOnlyList<string>> ExportAsync(
        string outputDirectory,
        Catalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(catalog);

        var files = catalog.RequiredLocales.Select(locale => (
            locale,
            ApprovedTexts(catalog, locale)));

        return LocaleJsonFileWriter.WriteFilesAsync(outputDirectory, files, cancellationToken);
    }

    private static IEnumerable<KeyValuePair<string, string>> ApprovedTexts(
        Catalog catalog,
        Core.Identity.Locale locale)
    {
        foreach (var entry in catalog.Entries.Values)
        {
            if (catalog.GetEffectiveStatus(entry, locale) == TranslationEffectiveStatus.Approved
                && entry.Translations.TryGetValue(locale, out var translation))
            {
                yield return new KeyValuePair<string, string>(entry.Key.Value, translation.Text);
            }
        }
    }
}
