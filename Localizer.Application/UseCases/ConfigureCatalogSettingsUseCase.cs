using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;

namespace Localizer.Application.UseCases;

public sealed record ConfigureCatalogSettingsRequest
{
    public required string SourceLocale { get; init; }

    public required IReadOnlyList<string> RequiredLocales { get; init; }

    public MessageSyntaxProfile? DefaultSyntaxProfile { get; init; }
}

public sealed class ConfigureCatalogSettingsUseCase
{
    public UseCaseResult Execute(Catalog catalog, ConfigureCatalogSettingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        return DomainGuard.Try(() =>
        {
            var requiredLocales = request.RequiredLocales
                .Select(Locale.Create)
                .ToList();

            catalog.ConfigureLocales(Locale.Create(request.SourceLocale), requiredLocales);

            if (request.DefaultSyntaxProfile is { } profile)
            {
                catalog.SetDefaultSyntaxProfile(profile);
            }
        });
    }
}
