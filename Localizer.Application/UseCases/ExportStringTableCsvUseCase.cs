using Localizer.Application.Export;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;

namespace Localizer.Application.UseCases;

public sealed record ExportStringTableCsvOutcome
{
    public required bool Succeeded { get; init; }

    public string? WrittenFilePath { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class ExportStringTableCsvUseCase
{
    private readonly IStringTableCsvExporter _exporter;

    public ExportStringTableCsvUseCase(IStringTableCsvExporter exporter)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    public async Task<ExportStringTableCsvOutcome> ExecuteAsync(
        Catalog catalog,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await _exporter.ExportAsync(path, catalog, cancellationToken).ConfigureAwait(false);
            return new ExportStringTableCsvOutcome
            {
                Succeeded = true,
                WrittenFilePath = path
            };
        }
        catch (CatalogPersistenceException exception)
        {
            return new ExportStringTableCsvOutcome
            {
                Succeeded = false,
                ErrorCode = exception.Code,
                ErrorMessage = exception.Message
            };
        }
    }
}
