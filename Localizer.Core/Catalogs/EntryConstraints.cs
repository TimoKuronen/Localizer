using Localizer.Core.Lifecycle;

namespace Localizer.Core.Catalogs;

public sealed record EntryConstraints
{
    public int? MaxGraphemes { get; init; }
    public int? MaxUtf8Bytes { get; init; }
    public int? MaxLines { get; init; }
    public IReadOnlyList<string> RequiredTerms { get; init; } = [];
    public IReadOnlyList<string> ForbiddenTerms { get; init; } = [];
}
