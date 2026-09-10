using Localizer.Core.Catalogs;
using Localizer.Core.Validation;

namespace Localizer.Application.UseCases;

public sealed class ValidateCatalogUseCase
{
    public ValidationResult Execute(Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return CatalogValidator.Validate(catalog);
    }
}
