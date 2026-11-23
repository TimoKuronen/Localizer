namespace Localizer.Application.Import;

public interface IStringTableCsvReader
{
    Task<StringTableCsvDocument> ReadAsync(
        string path,
        string sourceLocale,
        CancellationToken cancellationToken = default);
}
