using Localizer.Application.Import;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;

namespace Localizer.Application.UseCases;

public sealed record ImportUnityCsvOutcome
{
    public required bool Succeeded { get; init; }

    public required MergeUnityCsvImportOutcome MergeOutcome { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class ImportUnityCsvUseCase
{
    private readonly IUnityCsvReader _reader;
    private readonly MergeUnityCsvImportUseCase _merge;

    public ImportUnityCsvUseCase(IUnityCsvReader reader, MergeUnityCsvImportUseCase merge)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _merge = merge ?? throw new ArgumentNullException(nameof(merge));
    }

    public async Task<ImportUnityCsvOutcome> ExecuteAsync(
        Catalog catalog,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var document = await _reader.ReadAsync(path, cancellationToken).ConfigureAwait(false);
            var mergeOutcome = _merge.Execute(catalog, document);

            return new ImportUnityCsvOutcome
            {
                Succeeded = mergeOutcome.Succeeded,
                MergeOutcome = mergeOutcome,
                ErrorCode = mergeOutcome.ErrorCode,
                ErrorMessage = mergeOutcome.ErrorMessage
            };
        }
        catch (CatalogPersistenceException exception)
        {
            return new ImportUnityCsvOutcome
            {
                Succeeded = false,
                MergeOutcome = new MergeUnityCsvImportOutcome
                {
                    Succeeded = false,
                    AddedCount = 0,
                    UpdatedCount = 0,
                    UnchangedCount = 0
                },
                ErrorCode = exception.Code,
                ErrorMessage = exception.Message
            };
        }
    }
}
