namespace Localizer.Application.Import;

public interface IUnityCsvReader
{
    Task<UnityCsvDocument> ReadAsync(
        string path,
        string sourceLocale,
        CancellationToken cancellationToken = default);
}
