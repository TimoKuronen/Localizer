using Localizer.Core.Catalogs;
using Localizer.Core.Errors;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Validation;

namespace Localizer.Application.UseCases;

public sealed record ApproveMatchingTranslationsRequest
{
    public required string SourceKey { get; init; }

    public required string Locale { get; init; }

    public required IReadOnlyList<string> TargetKeys { get; init; }
}

public sealed record ApproveMatchingTranslationsOutcome
{
    public required IReadOnlyList<string> ApprovedKeys { get; init; }

    /// <summary>
    /// One message per target that was left unchanged.
    /// </summary>
    public required IReadOnlyList<string> BlockedMessages { get; init; }
}

/// <summary>
/// Copies an Approved translation to chosen entries with the same source and approves each one that
/// passes validation. A target that is blocked, does not match, or is already Approved is unchanged.
/// </summary>
public sealed class ApproveMatchingTranslationsUseCase
{
    public UseCaseResult<ApproveMatchingTranslationsOutcome> Execute(
        Catalog catalog,
        ApproveMatchingTranslationsRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        return DomainGuard.Try(() =>
        {
            var locale = Locale.Create(request.Locale);

            if (!catalog.Entries.TryGetValue(EntryKey.Create(request.SourceKey), out var source))
            {
                throw new DomainValidationException(
                    UseCaseErrorCodes.EntryNotFound,
                    $"No entry with key '{request.SourceKey}' exists.");
            }

            if (catalog.GetEffectiveStatus(source, locale) != TranslationEffectiveStatus.Approved)
            {
                throw new DomainValidationException(
                    UseCaseErrorCodes.TranslationNotApproved,
                    $"Translation for key '{request.SourceKey}' and locale '{locale.Value}' is not Approved.");
            }

            var approved = new List<string>();
            var blocked = new List<string>();

            foreach (var targetKey in request.TargetKeys)
            {
                var failure = TryApprove(catalog, source, locale, targetKey);
                if (failure is null)
                {
                    approved.Add(targetKey);
                }
                else
                {
                    blocked.Add($"{targetKey}: {failure}");
                }
            }

            return new ApproveMatchingTranslationsOutcome
            {
                ApprovedKeys = approved,
                BlockedMessages = blocked
            };
        });
    }

    // Returns why the target was left unchanged, or null once it is approved.
    private static string? TryApprove(Catalog catalog, CatalogEntry source, Locale locale, string targetKey)
    {
        if (!catalog.Entries.TryGetValue(EntryKey.Create(targetKey), out var target))
        {
            return "The entry does not exist.";
        }

        if (!RepeatedSources.IsSameSource(catalog, source, target))
        {
            return "The source text differs.";
        }

        if (catalog.GetEffectiveStatus(target, locale) == TranslationEffectiveStatus.Approved)
        {
            return "The translation is already Approved.";
        }

        var translations = new Dictionary<Locale, Translation>(target.Translations)
        {
            [locale] = source.Translations[locale] with
            {
                BasedOnFingerprint = catalog.GetCurrentFingerprint(target)
            }
        };
        var candidate = target with { Translations = translations };

        var validation = ApprovalValidationPolicy.Evaluate(catalog, candidate, locale);
        if (validation.HasBlockingErrors)
        {
            var first = validation.Diagnostics[0];
            return $"{first.Code}: {first.Message}";
        }

        try
        {
            catalog.ReplaceEntry(candidate);
        }
        catch (DomainValidationException exception)
        {
            return exception.Message;
        }

        return null;
    }
}
