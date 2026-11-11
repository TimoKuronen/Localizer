using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;

namespace Localizer.Desktop.Tests.Fakes;

internal sealed class InMemoryCatalogStore : ICatalogStore
{
    private readonly Dictionary<string, Catalog> _catalogs = new(StringComparer.Ordinal);

    public List<string> SavedPaths { get; } = [];

    public void Put(string path, Catalog catalog) => _catalogs[path] = catalog;

    public Task<Catalog> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        _catalogs.TryGetValue(path, out var catalog)
            ? Task.FromResult(catalog)
            : throw new FileNotFoundException("No catalog at path.", path);

    public Task SaveAsync(string path, Catalog catalog, CancellationToken cancellationToken = default)
    {
        _catalogs[path] = catalog;
        SavedPaths.Add(path);
        return Task.CompletedTask;
    }
}
