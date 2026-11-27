using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;

namespace Localizer.Application.UseCases;

/// <summary>
/// Lists the entries that repeat an entry's source and still need approval for a locale.
/// Currently Approved translations are never listed.
/// </summary>
public sealed class FindMatchingTranslationsUseCase
{
    public IReadOnlyList<MatchingTranslation> Execute(Catalog catalog, string key, string locale)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        if (!catalog.Entries.TryGetValue(EntryKey.Create(key), out var source))
        {
            return [];
        }

        var targetLocale = Locale.Create(locale);
        var fingerprint = catalog.GetCurrentFingerprint(source);
        var matches = new List<MatchingTranslation>();

        foreach (var entry in catalog.Entries.Values.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal))
        {
            if (entry.Key.Equals(source.Key) || !RepeatedSources.IsSameSource(catalog, source, entry))
            {
                continue;
            }

            var status = catalog.GetEffectiveStatus(entry, targetLocale);
            if (status == TranslationEffectiveStatus.Approved)
            {
                continue;
            }

            entry.Translations.TryGetValue(targetLocale, out var translation);
            matches.Add(new MatchingTranslation
            {
                Key = entry.Key.Value,
                Status = status,
                CurrentText = translation?.Text,
                DeveloperNotes = entry.DeveloperNotes,
                IsIdentical = catalog.GetCurrentFingerprint(entry).Equals(fingerprint),
                WasApproved = translation?.State == TranslationState.Approved
            });
        }

        return matches;
    }
}
