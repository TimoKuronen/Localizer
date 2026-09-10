using Localizer.Core.Catalogs;
using Localizer.Core.Identity;

namespace Localizer.Application.UseCases;

public sealed class RemoveCatalogEntryUseCase
{
    public UseCaseResult Execute(Catalog catalog, string key)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return DomainGuard.Try(() =>
        {
            catalog.RemoveEntry(EntryKey.Create(key));
        });
    }
}
