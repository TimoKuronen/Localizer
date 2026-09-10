using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;

namespace Localizer.Application.Tests;

internal sealed class InMemoryCatalogStore : ICatalogStore
{
    private readonly Dictionary<string, Catalog> _catalogs = new(StringComparer.Ordinal);

    public Task<Catalog> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!_catalogs.TryGetValue(path, out var catalog))
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.IoFailed,
                $"Catalog file '{path}' was not found.");
        }

        return Task.FromResult(catalog);
    }

    public Task SaveAsync(string path, Catalog catalog, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(catalog);

        _catalogs[path] = catalog;
        return Task.CompletedTask;
    }
}
