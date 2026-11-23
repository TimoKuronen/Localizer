namespace Localizer.Application.Drafting;

public sealed record DraftConstraintHints
{
    public int? MaxGraphemes { get; init; }

    public int? MaxUtf8Bytes { get; init; }

    public int? MaxLines { get; init; }

    public IReadOnlyList<string> RequiredTerms { get; init; } = [];

    public IReadOnlyList<string> ForbiddenTerms { get; init; } = [];
}
