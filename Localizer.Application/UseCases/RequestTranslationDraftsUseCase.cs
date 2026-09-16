using Localizer.Application.Drafting;
using Localizer.Application.Time;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;

namespace Localizer.Application.UseCases;

public sealed record RequestTranslationDraftsRequest
{
    public WorkQueueFilter Filter { get; init; } = WorkQueueFilter.Missing | WorkQueueFilter.Stale;
}

public sealed record RequestTranslationDraftsOutcome
{
    public required int RequestedCount { get; init; }

    public required int AppliedCount { get; init; }

    public required IReadOnlyList<string> RejectionMessages { get; init; }
}

public sealed class RequestTranslationDraftsUseCase
{
    private readonly ITranslationDraftProvider _draftProvider;
    private readonly IClock _clock;
    private readonly GetWorkQueueUseCase _getWorkQueue = new();
    private readonly SetTranslationDraftUseCase _setTranslationDraft = new();

    public RequestTranslationDraftsUseCase(ITranslationDraftProvider draftProvider, IClock clock)
    {
        _draftProvider = draftProvider ?? throw new ArgumentNullException(nameof(draftProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<UseCaseResult<RequestTranslationDraftsOutcome>> ExecuteAsync(
        Catalog catalog,
        RequestTranslationDraftsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        var filter = request.Filter & (WorkQueueFilter.Missing | WorkQueueFilter.Stale);
        if (filter == WorkQueueFilter.None)
        {
            return UseCaseResult<RequestTranslationDraftsOutcome>.Failure(
                UseCaseErrorCodes.InvalidArgument,
                "Drafting filter must include Missing and/or Stale.");
        }

        var queue = _getWorkQueue.Execute(catalog, filter);
        if (queue.Count == 0)
        {
            return UseCaseResult<RequestTranslationDraftsOutcome>.Success(new RequestTranslationDraftsOutcome
            {
                RequestedCount = 0,
                AppliedCount = 0,
                RejectionMessages = []
            });
        }

        var draftItems = new List<TranslationDraftItemRequest>(queue.Count);
        var expectedKeys = new HashSet<(string Key, string Locale)>();

        foreach (var item in queue)
        {
            if (!catalog.Entries.TryGetValue(item.Key, out var entry))
            {
                continue;
            }

            var syntaxProfile = catalog.GetEffectiveSyntaxProfile(entry);
            draftItems.Add(new TranslationDraftItemRequest
            {
                Key = entry.Key.Value,
                TargetLocale = item.Locale.Value,
                SourceLocale = catalog.SourceLocale.Value,
                SourceText = entry.SourceText,
                DeveloperNotes = entry.DeveloperNotes,
                Context = entry.Context.Values,
                Constraints = MapConstraints(entry.Constraints),
                SyntaxProfile = MessageSyntaxProfileNames.ToName(syntaxProfile)
            });
            expectedKeys.Add((entry.Key.Value, item.Locale.Value));
        }

        TranslationDraftBatchResult batch;
        try
        {
            batch = await _draftProvider
                .DraftAsync(new TranslationDraftBatchRequest { Items = draftItems }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UseCaseResult<RequestTranslationDraftsOutcome>.Failure(
                UseCaseErrorCodes.DraftProviderFailed,
                $"Draft provider failed: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(batch.ProviderName) || string.IsNullOrWhiteSpace(batch.ModelName))
        {
            return UseCaseResult<RequestTranslationDraftsOutcome>.Failure(
                UseCaseErrorCodes.DraftProviderInvalidResponse,
                "Draft provider must report provider and model names.");
        }

        var rejections = new List<string>(batch.ItemFailures);
        var seen = new HashSet<(string Key, string Locale)>();
        var applied = 0;
        var generatedAt = _clock.UtcNow;

        foreach (var result in batch.Items)
        {
            if (string.IsNullOrWhiteSpace(result.Key) || string.IsNullOrWhiteSpace(result.TargetLocale))
            {
                rejections.Add("Rejected draft with missing key or locale.");
                continue;
            }

            var pair = (result.Key, result.TargetLocale);
            if (!expectedKeys.Contains(pair))
            {
                rejections.Add($"Rejected unexpected draft for '{result.Key}' ({result.TargetLocale}).");
                continue;
            }

            if (!seen.Add(pair))
            {
                rejections.Add($"Rejected duplicate draft for '{result.Key}' ({result.TargetLocale}).");
                continue;
            }

            if (result.Text is null)
            {
                rejections.Add($"Rejected empty text payload for '{result.Key}' ({result.TargetLocale}).");
                continue;
            }

            var setResult = _setTranslationDraft.Execute(catalog, new SetTranslationDraftRequest
            {
                Key = result.Key,
                Locale = result.TargetLocale,
                Text = result.Text,
                Origin = TranslationOrigin.Model,
                ProviderName = batch.ProviderName,
                ModelName = batch.ModelName,
                GeneratedAtUtc = generatedAt
            });

            if (!setResult.Succeeded)
            {
                rejections.Add(
                    $"Failed to store draft for '{result.Key}' ({result.TargetLocale}): {setResult.ErrorMessage}");
                continue;
            }

            applied++;
        }

        return UseCaseResult<RequestTranslationDraftsOutcome>.Success(new RequestTranslationDraftsOutcome
        {
            RequestedCount = draftItems.Count,
            AppliedCount = applied,
            RejectionMessages = rejections
        });
    }

    private static DraftConstraintHints MapConstraints(EntryConstraints constraints) =>
        new()
        {
            MaxGraphemes = constraints.MaxGraphemes,
            MaxUtf8Bytes = constraints.MaxUtf8Bytes,
            MaxLines = constraints.MaxLines,
            RequiredTerms = constraints.RequiredTerms,
            ForbiddenTerms = constraints.ForbiddenTerms
        };
}
