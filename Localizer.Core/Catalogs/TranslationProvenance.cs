using Localizer.Core.Lifecycle;

namespace Localizer.Core.Catalogs;

public sealed record TranslationProvenance
{
    public required TranslationOrigin Origin { get; init; }
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public DateTimeOffset? GeneratedAtUtc { get; init; }
}
