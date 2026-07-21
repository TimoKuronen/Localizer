using Localizer.Core.Catalogs;

namespace Localizer.Application.Persistence;

public interface ICatalogStore
{
    Task<Catalog> LoadAsync(string path, CancellationToken cancellationToken = default);

    Task SaveAsync(string path, Catalog catalog, CancellationToken cancellationToken = default);
}
