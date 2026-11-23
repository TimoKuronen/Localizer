namespace Localizer.Application.Drafting;

public sealed record TranslationDraftItemResult
{
    public required string Key { get; init; }

    public required string TargetLocale { get; init; }

    public required string Text { get; init; }
}
