using System.Text;
using Localizer.Application.Export;
using Localizer.Application.Import;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Infrastructure.Import;

namespace Localizer.Infrastructure.Export;

public sealed class UnityCsvExporter : IUnityCsvExporter
{
    public async Task ExportAsync(string path, Catalog catalog, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(catalog);

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.IoFailed,
                $"Unity CSV path '{path}' does not include a directory.");
        }

        var builder = new StringBuilder();
        var headers = new List<string> { "Key", "Id", UnityCsvParser.FormatHeader(catalog.SourceLocale.Value, isSourceLocale: true) };
        headers.AddRange(catalog.RequiredLocales.Select(locale => UnityCsvParser.FormatHeader(locale.Value, isSourceLocale: false)));

        builder.AppendLine(string.Join(",", headers.Select(UnityCsvParser.EscapeField)));

        foreach (var entry in catalog.Entries.Values.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fields = new List<string>
            {
                entry.Key.Value,
                entry.ExternalIds.Values.GetValueOrDefault(UnityCsvBridgeConstants.UnityExternalIdNamespace) ?? string.Empty,
                entry.SourceText
            };

            foreach (var locale in catalog.RequiredLocales)
            {
                fields.Add(GetExportTranslationText(catalog, entry, locale));
            }

            builder.AppendLine(string.Join(",", fields.Select(UnityCsvParser.EscapeField)));
        }

        var csv = builder.ToString();
        if (!csv.EndsWith('\n'))
        {
            csv += '\n';
        }

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(csv);
        var tempPath = path + ".tmp";

        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteFile(tempPath);
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.IoFailed,
                $"Failed to export Unity CSV file '{path}'.",
                exception);
        }
    }

    private static string GetExportTranslationText(Catalog catalog, CatalogEntry entry, Locale locale)
    {
        if (catalog.GetEffectiveStatus(entry, locale) != TranslationEffectiveStatus.Approved)
        {
            return string.Empty;
        }

        return entry.Translations.TryGetValue(locale, out var translation)
            ? translation.Text
            : string.Empty;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
