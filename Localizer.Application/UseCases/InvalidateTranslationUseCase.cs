using Localizer.Core.Catalogs;
using Localizer.Core.Errors;
using Localizer.Core.Fingerprints;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;

namespace Localizer.Application.UseCases;

public sealed record InvalidateTranslationRequest
{
    public required string Key { get; init; }

    public required string Locale { get; init; }
}

public sealed class InvalidateTranslationUseCase
{
    internal static readonly Fingerprint InvalidatedFingerprint =
        Fingerprint.Create("localizer:invalidated-translation");

    public UseCaseResult Execute(Catalog catalog, InvalidateTranslationRequest request)
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
            if (effectiveStatus is TranslationEffectiveStatus.Missing)
            {
                throw new DomainValidationException(
                    UseCaseErrorCodes.TranslationNotFound,
                    $"Translation for key '{request.Key}' and locale '{locale.Value}' is missing.");
            }

            if (effectiveStatus is TranslationEffectiveStatus.Stale)
            {
                return;
            }

            var invalidatedFingerprint = translation.BasedOnFingerprint.Equals(InvalidatedFingerprint)
                ? Fingerprint.Create("localizer:invalidated-translation:2")
                : InvalidatedFingerprint;

            var translations = new Dictionary<Locale, Translation>(existing.Translations)
            {
                [locale] = translation with { BasedOnFingerprint = invalidatedFingerprint }
            };

            catalog.ReplaceEntry(existing with { Translations = translations });
        });
    }
}
