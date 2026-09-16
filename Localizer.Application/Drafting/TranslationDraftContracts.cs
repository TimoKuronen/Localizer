namespace Localizer.Application.Drafting;

public sealed record DraftConstraintHints
{
    public int? MaxGraphemes { get; init; }

    public int? MaxUtf8Bytes { get; init; }

    public int? MaxLines { get; init; }

    public IReadOnlyList<string> RequiredTerms { get; init; } = [];

    public IReadOnlyList<string> ForbiddenTerms { get; init; } = [];
}

public sealed record TranslationDraftItemRequest
{
    public required string Key { get; init; }

    public required string TargetLocale { get; init; }

    public required string SourceLocale { get; init; }

    public required string SourceText { get; init; }

    public string? DeveloperNotes { get; init; }

    public IReadOnlyDictionary<string, string> Context { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public DraftConstraintHints Constraints { get; init; } = new();

    public required string SyntaxProfile { get; init; }
}

public sealed record TranslationDraftBatchRequest
{
    public required IReadOnlyList<TranslationDraftItemRequest> Items { get; init; }
}

public sealed record TranslationDraftItemResult
{
    public required string Key { get; init; }

    public required string TargetLocale { get; init; }

    public required string Text { get; init; }
}

public sealed record TranslationDraftBatchResult
{
    public required string ProviderName { get; init; }

    public required string ModelName { get; init; }

    public required IReadOnlyList<TranslationDraftItemResult> Items { get; init; }

    public IReadOnlyList<string> ItemFailures { get; init; } = [];
}

public interface ITranslationDraftProvider
{
    Task<TranslationDraftBatchResult> DraftAsync(
        TranslationDraftBatchRequest request,
        CancellationToken cancellationToken = default);
}
