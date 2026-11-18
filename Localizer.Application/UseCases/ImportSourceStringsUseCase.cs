using Localizer.Application.Import;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;
using Localizer.Core.Errors;

namespace Localizer.Application.UseCases;

public sealed class ImportSourceStringsUseCase
{
    private readonly ISourceStringsReader _reader;
    private readonly MergeSourceStringsUseCase _merge;

    public ImportSourceStringsUseCase(ISourceStringsReader reader, MergeSourceStringsUseCase merge)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _merge = merge ?? throw new ArgumentNullException(nameof(merge));
    }

    public async Task<SourceStringsImportOutcome> ExecuteAsync(
        Catalog catalog,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var document = await _reader.ReadAsync(path, cancellationToken).ConfigureAwait(false);
            return _merge.Execute(catalog, document);
        }
        catch (CatalogPersistenceException exception)
        {
            return SourceStringsImportOutcome.Failure(exception.Code, exception.Message);
        }
        catch (DomainValidationException exception)
        {
            return SourceStringsImportOutcome.Failure(exception.Code, exception.Message);
        }
    }
}
