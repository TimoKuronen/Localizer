using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;

namespace Localizer.Application.UseCases;

public sealed record SetTranslationDraftRequest
{
    public required string Key { get; init; }

    public required string Locale { get; init; }

    public required string Text { get; init; }

    public DateTimeOffset? GeneratedAtUtc { get; init; }
}

public sealed class SetTranslationDraftUseCase
{
    public UseCaseResult Execute(Catalog catalog, SetTranslationDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        return DomainGuard.Try(() =>
        {
            var key = EntryKey.Create(request.Key);
            var locale = Locale.Create(request.Locale);

            if (!catalog.Entries.TryGetValue(key, out var existing))
            {
                throw new Core.Errors.DomainValidationException(
                    UseCaseErrorCodes.EntryNotFound,
                    $"No entry with key '{request.Key}' exists.");
            }

            if (!catalog.RequiredLocales.Any(required => required.Value.Equals(locale.Value, StringComparison.Ordinal)))
            {
                throw new Core.Errors.DomainValidationException(
                    UseCaseErrorCodes.LocaleNotRequired,
                    $"Locale '{locale.Value}' is not a required target locale for this catalog.");
            }

            var fingerprint = catalog.GetCurrentFingerprint(existing);
            var translations = new Dictionary<Locale, Translation>(existing.Translations)
            {
                [locale] = new Translation
                {
                    Text = request.Text,
                    State = TranslationState.Draft,
                    BasedOnFingerprint = fingerprint,
                    Provenance = new TranslationProvenance
                    {
                        Origin = TranslationOrigin.Human,
                        GeneratedAtUtc = request.GeneratedAtUtc
                    }
                }
            };

            catalog.ReplaceEntry(existing with { Translations = translations });
        });
    }
}
