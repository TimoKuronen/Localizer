using System.Text;
using Localizer.Application.Import;
using Localizer.Application.Persistence;

namespace Localizer.Infrastructure.Import;

public sealed class UnityCsvReader : IUnityCsvReader
{
    public async Task<UnityCsvDocument> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            EnsureNoByteOrderMark(bytes);
            var csvText = Encoding.UTF8.GetString(bytes);
            return UnityCsvParser.Parse(csvText);
        }
        catch (CatalogPersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.IoFailed,
                $"Failed to read Unity CSV file '{path}'.",
                exception);
        }
    }

    private static void EnsureNoByteOrderMark(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.ByteOrderMarkPresent,
                "Unity CSV must be UTF-8 without a byte order mark.");
        }
    }
}
