using System.Text;
using System.Text.Json;
using Localizer.Application.Persistence;
using Localizer.Core.Identity;

namespace Localizer.Infrastructure.Export;

internal static class LocaleJsonFileWriter
{
    public static async Task<IReadOnlyList<string>> WriteFilesAsync(
        string outputDirectory,
        IEnumerable<(Locale Locale, IEnumerable<KeyValuePair<string, string>> Texts)> files,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);

            var writtenFiles = new List<string>();
            foreach (var (locale, texts) in files.OrderBy(file => file.Locale.Value, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var path = Path.Combine(outputDirectory, $"{locale.Value}.json");
                await WriteFileAsync(path, texts, cancellationToken).ConfigureAwait(false);
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

    private static async Task WriteFileAsync(
        string path,
        IEnumerable<KeyValuePair<string, string>> texts,
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

            foreach (var (key, text) in texts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteString(key, text);
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
