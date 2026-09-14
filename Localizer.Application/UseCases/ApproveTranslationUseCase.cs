using Localizer.Core.Catalogs;
using Localizer.Core.Errors;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Validation;

namespace Localizer.Application.UseCases;

public sealed record ApproveTranslationRequest
{
    public required string Key { get; init; }

    public required string Locale { get; init; }
}

public sealed class ApproveTranslationUseCase
{
    public UseCaseResult Execute(Catalog catalog, ApproveTranslationRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        return DomainGuard.Try(() =>
        {
            var key = EntryKey.Create(request.Key);
            var locale = Locale.Create(request.Locale);

            if (!catalog.Entries.TryGetValue(key, out var existing))
            {
                throw new DomainValidationException(
                    UseCaseErrorCodes.EntryNotFound,
                    $"No entry with key '{request.Key}' exists.");
            }

            if (!catalog.RequiredLocales.Any(required => required.Value.Equals(locale.Value, StringComparison.Ordinal)))
            {
                throw new DomainValidationException(
                    UseCaseErrorCodes.LocaleNotRequired,
                    $"Locale '{locale.Value}' is not a required target locale for this catalog.");
            }

            if (!existing.Translations.TryGetValue(locale, out var translation))
            {
                throw new DomainValidationException(
                    UseCaseErrorCodes.TranslationNotFound,
                    $"No translation exists for key '{request.Key}' and locale '{locale.Value}'.");
            }

            var effectiveStatus = catalog.GetEffectiveStatus(existing, locale);
            if (effectiveStatus != TranslationEffectiveStatus.Draft)
            {
                throw new DomainValidationException(
                    UseCaseErrorCodes.TranslationNotDraft,
                    $"Translation for key '{request.Key}' and locale '{locale.Value}' is '{effectiveStatus}' and cannot be approved.");
            }

            var approvalValidation = ApprovalValidationPolicy.Evaluate(catalog, existing, locale);
            if (approvalValidation.HasBlockingErrors)
            {
                var first = approvalValidation.Diagnostics[0];
                throw new DomainValidationException(
                    UseCaseErrorCodes.ValidationBlocked,
                    $"Translation for key '{request.Key}' and locale '{locale.Value}' failed approval validation ({first.Code}: {first.Message}).");
            }

            var translations = new Dictionary<Locale, Translation>(existing.Translations)
            {
                [locale] = translation with { State = TranslationState.Approved }
            };

            catalog.ReplaceEntry(existing with { Translations = translations });
        });
    }
}
