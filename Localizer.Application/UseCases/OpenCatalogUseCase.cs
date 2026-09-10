using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;

namespace Localizer.Application.UseCases;

public sealed class OpenCatalogUseCase
{
    private readonly ICatalogStore _catalogStore;

    public OpenCatalogUseCase(ICatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public async Task<UseCaseResult<Catalog>> ExecuteAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var catalog = await _catalogStore.LoadAsync(path, cancellationToken).ConfigureAwait(false);
            return UseCaseResult<Catalog>.Success(catalog);
        }
        catch (CatalogPersistenceException exception)
        {
            return UseCaseResult<Catalog>.Failure(exception.Code, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return UseCaseResult<Catalog>.Failure(UseCaseErrorCodes.InvalidArgument, exception.Message);
        }
    }
}
