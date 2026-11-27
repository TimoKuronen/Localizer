using Localizer.Application.Drafting;
using Localizer.Application.Time;
using Localizer.Core.Catalogs;
using Localizer.Core.Fingerprints;
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

    /// <summary>
    /// Applied drafts copied from an identical Approved translation instead of generated.
    /// </summary>
    public int ReusedCount { get; init; }

    public required IReadOnlyList<string> RejectionMessages { get; init; }
}

/// <summary>
/// Drafts the queued translations. Entries with identical translation inputs share one provider
/// request per locale. When Approved translations are not themselves queued, an identical Approved
/// translation is copied as a Draft, unless it equals the text the target already holds.
/// </summary>
public sealed class RequestTranslationDraftsUseCase
{
    private readonly ITranslationDraftProvider _draftProvider;
    private readonly IClock _clock;
    private readonly GetWorkQueueUseCase _getWorkQueue;
    private readonly SetTranslationDraftUseCase _setTranslationDraft;

    public RequestTranslationDraftsUseCase(
        ITranslationDraftProvider draftProvider,
        IClock clock,
        GetWorkQueueUseCase getWorkQueue,
        SetTranslationDraftUseCase setTranslationDraft)
    {
        _draftProvider = draftProvider ?? throw new ArgumentNullException(nameof(draftProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _getWorkQueue = getWorkQueue ?? throw new ArgumentNullException(nameof(getWorkQueue));
        _setTranslationDraft = setTranslationDraft ?? throw new ArgumentNullException(nameof(setTranslationDraft));
    }

    public async Task<UseCaseResult<RequestTranslationDraftsOutcome>> ExecuteAsync(
        Catalog catalog,
        RequestTranslationDraftsRequest request,
        IProgress<TranslationDraftProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        var filter = request.Filter;
        if (filter == WorkQueueFilter.None)
        {
            return UseCaseResult<RequestTranslationDraftsOutcome>.Failure(
                UseCaseErrorCodes.InvalidArgument,
                "Drafting filter must include at least one status.");
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

        var approved = filter.HasFlag(WorkQueueFilter.Approved)
            ? new Dictionary<(Fingerprint, Locale), Translation>()
            : FindApprovedTranslations(catalog);
        var draftItems = new List<TranslationDraftItemRequest>(queue.Count);
        var reuses = new List<(string Key, string Locale, Translation Source)>();
        // Keys sharing each requested item, the requested key first.
        var sharedKeys = new Dictionary<(string Key, string Locale), List<string>>();
        var requestedGroups = new Dictionary<(Fingerprint, Locale), string>();
        var requestedCount = 0;

        foreach (var item in queue)
        {
            if (!catalog.Entries.TryGetValue(item.Key, out var entry))
            {
                continue;
            }

            requestedCount++;
            var group = (catalog.GetCurrentFingerprint(entry), item.Locale);

            // Equal text means the copy would repeat what is being drafted again.
            if (approved.TryGetValue(group, out var match)
                && !string.Equals(
                    entry.Translations.GetValueOrDefault(item.Locale)?.Text,
                    match.Text,
                    StringComparison.Ordinal))
            {
                reuses.Add((entry.Key.Value, item.Locale.Value, match));
                continue;
            }

            if (requestedGroups.TryGetValue(group, out var requestedKey))
            {
                sharedKeys[(requestedKey, item.Locale.Value)].Add(entry.Key.Value);
                continue;
            }

            requestedGroups.Add(group, entry.Key.Value);
            sharedKeys.Add((entry.Key.Value, item.Locale.Value), [entry.Key.Value]);

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
        }

        TranslationDraftBatchResult? batch = null;
        try
        {
            if (draftItems.Count > 0)
            {
                batch = await _draftProvider
                    .DraftAsync(
                        new TranslationDraftBatchRequest
                        {
                            Items = draftItems,
                            ProjectContext = catalog.ProjectContext,
                            Glossary = catalog.Glossary
                        },
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
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

        if (batch is not null
            && (string.IsNullOrWhiteSpace(batch.ProviderName) || string.IsNullOrWhiteSpace(batch.ModelName)))
        {
            return UseCaseResult<RequestTranslationDraftsOutcome>.Failure(
                UseCaseErrorCodes.DraftProviderInvalidResponse,
                "Draft provider must report provider and model names.");
        }

        var rejections = new List<string>(batch?.ItemFailures ?? []);
        var seen = new HashSet<(string Key, string Locale)>();
        var applied = 0;
        var reused = 0;
        var generatedAt = _clock.UtcNow;

        // Applied only after the provider succeeds, so a failed run leaves the catalog unchanged.
        foreach (var (key, locale, source) in reuses)
        {
            if (Store(catalog, new SetTranslationDraftRequest
                {
                    Key = key,
                    Locale = locale,
                    Text = source.Text,
                    Origin = source.Provenance.Origin,
                    ProviderName = source.Provenance.ProviderName,
                    ModelName = source.Provenance.ModelName,
                    GeneratedAtUtc = source.Provenance.GeneratedAtUtc
                }, rejections))
            {
                applied++;
                reused++;
            }
        }

        foreach (var result in batch?.Items ?? [])
        {
            if (string.IsNullOrWhiteSpace(result.Key) || string.IsNullOrWhiteSpace(result.TargetLocale))
            {
                rejections.Add("Rejected draft with missing key or locale.");
                continue;
            }

            var pair = (result.Key, result.TargetLocale);
            if (!sharedKeys.TryGetValue(pair, out var keys))
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

            foreach (var key in keys)
            {
                if (Store(catalog, new SetTranslationDraftRequest
                    {
                        Key = key,
                        Locale = result.TargetLocale,
                        Text = result.Text,
                        Origin = TranslationOrigin.Model,
                        ProviderName = batch!.ProviderName,
                        ModelName = batch.ModelName,
                        GeneratedAtUtc = generatedAt
                    }, rejections))
                {
                    applied++;
                }
            }
        }

        return UseCaseResult<RequestTranslationDraftsOutcome>.Success(new RequestTranslationDraftsOutcome
        {
            RequestedCount = requestedCount,
            AppliedCount = applied,
            ReusedCount = reused,
            RejectionMessages = rejections
        });
    }

    // The first Approved translation in key order stands for each set of identical inputs.
    private static Dictionary<(Fingerprint, Locale), Translation> FindApprovedTranslations(Catalog catalog)
    {
        var approved = new Dictionary<(Fingerprint, Locale), Translation>();
        foreach (var entry in catalog.Entries.Values.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal))
        {
            var fingerprint = catalog.GetCurrentFingerprint(entry);
            foreach (var (locale, translation) in entry.Translations)
            {
                if (catalog.GetEffectiveStatus(entry, locale) == TranslationEffectiveStatus.Approved)
                {
                    approved.TryAdd((fingerprint, locale), translation);
                }
            }
        }

        return approved;
    }

    private bool Store(Catalog catalog, SetTranslationDraftRequest request, List<string> rejections)
    {
        var result = _setTranslationDraft.Execute(catalog, request);
        if (!result.Succeeded)
        {
            rejections.Add(
                $"Failed to store draft for '{request.Key}' ({request.Locale}): {result.ErrorMessage}");
        }

        return result.Succeeded;
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
