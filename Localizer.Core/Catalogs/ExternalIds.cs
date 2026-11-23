using Localizer.Core.Lifecycle;

namespace Localizer.Core.Catalogs;

public sealed record ExternalIds
{
    public IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
