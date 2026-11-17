using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;

namespace Localizer.Application.Tests;

internal static class TestCatalogs
{
    public static Catalog CreatePlain() =>
        new(
            schemaVersion: 1,
            catalogId: CatalogId.Create("runner-game"),
            sourceLocale: Locale.Create("en"),
            requiredLocales: [Locale.Create("es")],
            defaultSyntaxProfile: MessageSyntaxProfile.Plain);
}
