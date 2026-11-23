using Localizer.Core.Lifecycle;

namespace Localizer.Core.Catalogs;

public sealed record TranslationContext
{
    public IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
