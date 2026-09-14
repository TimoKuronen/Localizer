using Localizer.Application.Export;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;
using Localizer.Core.Validation;

namespace Localizer.Application.UseCases;

public sealed record ExportCatalogOutcome
{
    public required bool Succeeded { get; init; }

    public required ValidationResult Validation { get; init; }

    public required IReadOnlyList<string> WrittenFiles { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class ExportCatalogUseCase
{
    private readonly ICatalogExporter _exporter;

    public ExportCatalogUseCase(ICatalogExporter exporter)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    public async Task<ExportCatalogOutcome> ExecuteAsync(
        Catalog catalog,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var validation = ExportValidationPolicy.Evaluate(catalog);
        if (validation.HasBlockingErrors)
        {
            return new ExportCatalogOutcome
            {
                Succeeded = false,
                Validation = validation,
                WrittenFiles = [],
                ErrorCode = UseCaseErrorCodes.ValidationBlocked,
                ErrorMessage = "Export is blocked until all required locales are approved and error-free."
            };
        }

        try
        {
            var writtenFiles = await _exporter
                .ExportAsync(outputDirectory, catalog, cancellationToken)
                .ConfigureAwait(false);

            return new ExportCatalogOutcome
            {
                Succeeded = true,
                Validation = validation,
                WrittenFiles = writtenFiles
            };
        }
        catch (CatalogPersistenceException exception)
        {
            return new ExportCatalogOutcome
            {
                Succeeded = false,
                Validation = validation,
                WrittenFiles = [],
                ErrorCode = exception.Code,
                ErrorMessage = exception.Message
            };
        }
        catch (ArgumentException exception)
        {
            return new ExportCatalogOutcome
            {
                Succeeded = false,
                Validation = validation,
                WrittenFiles = [],
                ErrorCode = UseCaseErrorCodes.InvalidArgument,
                ErrorMessage = exception.Message
            };
        }
    }
}
