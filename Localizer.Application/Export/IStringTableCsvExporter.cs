using Localizer.Core.Catalogs;

namespace Localizer.Application.Export;

public interface IStringTableCsvExporter
{
    Task ExportAsync(string path, Catalog catalog, CancellationToken cancellationToken = default);
}
