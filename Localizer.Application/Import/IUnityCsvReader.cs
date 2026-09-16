namespace Localizer.Application.Import;

public interface IUnityCsvReader
{
    Task<UnityCsvDocument> ReadAsync(string path, CancellationToken cancellationToken = default);
}
