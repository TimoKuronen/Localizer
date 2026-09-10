using Localizer.Core.Catalogs;
using Localizer.Core.Identity;

namespace Localizer.Application.UseCases;

public sealed class CreateCatalogUseCase
{
    public UseCaseResult<Catalog> Execute(CreateCatalogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return DomainGuard.Try(() =>
        {
            var requiredLocales = request.RequiredLocales
                .Select(Locale.Create)
                .ToList();

            return new Catalog(
                schemaVersion: request.SchemaVersion,
                catalogId: CatalogId.Create(request.CatalogId),
                sourceLocale: Locale.Create(request.SourceLocale),
                requiredLocales: requiredLocales,
                defaultSyntaxProfile: request.DefaultSyntaxProfile);
        });
    }
}
