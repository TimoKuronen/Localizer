using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;

namespace Localizer.Application.UseCases;

public sealed class SaveCatalogUseCase
{
    private readonly ICatalogStore _catalogStore;

    public SaveCatalogUseCase(ICatalogStore catalogStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
    }

    public async Task<UseCaseResult> ExecuteAsync(
        string path,
        Catalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        try
        {
            await _catalogStore.SaveAsync(path, catalog, cancellationToken).ConfigureAwait(false);
            return UseCaseResult.Success();
        }
        catch (CatalogPersistenceException exception)
        {
            return UseCaseResult.Failure(exception.Code, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return UseCaseResult.Failure(UseCaseErrorCodes.InvalidArgument, exception.Message);
        }
    }
}
