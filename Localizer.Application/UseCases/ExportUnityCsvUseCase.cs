using Localizer.Application.Export;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;

namespace Localizer.Application.UseCases;

public sealed record ExportUnityCsvOutcome
{
    public required bool Succeeded { get; init; }

    public string? WrittenFilePath { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class ExportUnityCsvUseCase
{
    private readonly IUnityCsvExporter _exporter;

    public ExportUnityCsvUseCase(IUnityCsvExporter exporter)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    public async Task<ExportUnityCsvOutcome> ExecuteAsync(
        Catalog catalog,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await _exporter.ExportAsync(path, catalog, cancellationToken).ConfigureAwait(false);
            return new ExportUnityCsvOutcome
            {
                Succeeded = true,
                WrittenFilePath = path
            };
        }
        catch (CatalogPersistenceException exception)
        {
            return new ExportUnityCsvOutcome
            {
                Succeeded = false,
                ErrorCode = exception.Code,
                ErrorMessage = exception.Message
            };
        }
    }
}
