using System.Text;
using System.Text.Json;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;

namespace Localizer.Infrastructure.Persistence.Json;

public sealed class JsonCatalogStore : ICatalogStore
{
    private readonly CatalogJsonMapper _mapper = new();
    private readonly JsonSerializerOptions _serializerOptions = CatalogJsonSerializerOptions.Create();

    public async Task<Catalog> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            EnsureNoByteOrderMark(bytes);

            CatalogDocumentDto document;
            try
            {
                document = JsonSerializer.Deserialize<CatalogDocumentDto>(bytes, _serializerOptions)
                           ?? throw new CatalogPersistenceException(
                               CatalogPersistenceErrorCodes.Malformed,
                               "Catalog JSON document was empty.");
            }
            catch (JsonException exception)
            {
                throw new CatalogPersistenceException(
                    CatalogPersistenceErrorCodes.Malformed,
                    "Catalog JSON document is malformed.",
                    exception);
            }

            if (document.SchemaVersion == 0
                || string.IsNullOrWhiteSpace(document.CatalogId)
                || string.IsNullOrWhiteSpace(document.SourceLocale))
            {
                var fileName = Path.GetFileName(path);
                var text = Encoding.UTF8.GetString(bytes);
                if (string.Equals(fileName, "project-folders.json", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("\"bindings\"", StringComparison.Ordinal))
                {
                    throw new CatalogPersistenceException(
                        CatalogPersistenceErrorCodes.Malformed,
                        $"'{fileName}' is Localizer project-folder settings, not a catalog. Use Save / Save As to create a catalog JSON (schemaVersion 1), then open that file.");
                }

                throw new CatalogPersistenceException(
                    CatalogPersistenceErrorCodes.Malformed,
                    $"'{fileName}' is not a Localizer catalog. Expected schemaVersion 1 with catalogId and sourceLocale. Save a catalog first, then open the saved .json file.");
            }

            return _mapper.ToDomain(document);
        }
        catch (CatalogPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.IoFailed,
                $"Failed to read catalog file '{path}'.",
                exception);
        }
    }

    public async Task SaveAsync(string path, Catalog catalog, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(catalog);

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.IoFailed,
                $"Catalog path '{path}' does not include a directory.");
        }

        var document = _mapper.ToDocument(catalog);
        var json = JsonSerializer.Serialize(document, _serializerOptions);
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
        catch (CatalogPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteFile(tempPath);
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.IoFailed,
                $"Failed to save catalog file '{path}'.",
                exception);
        }
    }

    private static void EnsureNoByteOrderMark(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.ByteOrderMarkPresent,
                "Catalog JSON must be UTF-8 without a byte order mark.");
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
