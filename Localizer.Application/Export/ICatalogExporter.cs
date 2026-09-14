using Localizer.Core.Catalogs;

namespace Localizer.Application.Export;

public interface ICatalogExporter
{
    Task<IReadOnlyList<string>> ExportAsync(
        string outputDirectory,
        Catalog catalog,
        CancellationToken cancellationToken = default);
}
