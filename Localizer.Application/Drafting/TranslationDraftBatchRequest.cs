namespace Localizer.Application.Drafting;

public sealed record TranslationDraftBatchRequest
{
    public required IReadOnlyList<TranslationDraftItemRequest> Items { get; init; }

    public string? ProjectContext { get; init; }

    public string? Glossary { get; init; }
}
