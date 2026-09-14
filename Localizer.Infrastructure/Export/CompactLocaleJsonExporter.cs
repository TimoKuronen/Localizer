using System.Text;
using System.Text.Json;
using Localizer.Application.Export;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;
using Localizer.Core.Lifecycle;

namespace Localizer.Infrastructure.Export;

public sealed class CompactLocaleJsonExporter : ICatalogExporter
{
    public async Task<IReadOnlyList<string>> ExportAsync(
        string outputDirectory,
        Catalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(catalog);

        try
        {
            Directory.CreateDirectory(outputDirectory);

            var writtenFiles = new List<string>();

            foreach (var locale in catalog.RequiredLocales.OrderBy(locale => locale.Value, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var path = Path.Combine(outputDirectory, $"{locale.Value}.json");
                await WriteLocaleFileAsync(path, catalog, locale, cancellationToken).ConfigureAwait(false);
                writtenFiles.Add(path);
            }

            return writtenFiles;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.IoFailed,
                $"Failed to export catalog to '{outputDirectory}'.",
                exception);
        }
    }

    private static async Task WriteLocaleFileAsync(
        string path,
        Catalog catalog,
        Core.Identity.Locale locale,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.IoFailed,
                $"Export path '{path}' does not include a directory.");
        }

        using var stream = new MemoryStream();
        await using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();

            foreach (var entry in catalog.Entries.Values.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal))
            {
                if (catalog.GetEffectiveStatus(entry, locale) != TranslationEffectiveStatus.Approved)
                {
                    continue;
                }

                if (!entry.Translations.TryGetValue(locale, out var translation))
                {
                    continue;
                }

                writer.WriteString(entry.Key.Value, translation.Text);
            }

            writer.WriteEndObject();
        }

        var json = Encoding.UTF8.GetString(stream.ToArray());
        if (!json.EndsWith('\n'))
        {
            json += '\n';
        }

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
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
            throw;
        }
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
