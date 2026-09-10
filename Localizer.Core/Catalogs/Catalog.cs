using Localizer.Core.Errors;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;

namespace Localizer.Core.Catalogs;

public sealed class Catalog
{
    private readonly Dictionary<EntryKey, CatalogEntry> _entries = new();

    public Catalog(
        int schemaVersion,
        CatalogId catalogId,
        Locale sourceLocale,
        IReadOnlyList<Locale> requiredLocales,
        MessageSyntaxProfile defaultSyntaxProfile)
    {
        if (schemaVersion <= 0)
        {
            throw new DomainValidationException(
                "catalog.schema_version.invalid",
                "Schema version must be a positive integer.");
        }

        if (requiredLocales.Count == 0)
        {
            throw new DomainValidationException(
                "catalog.required_locales.empty",
                "At least one required locale must be configured.");
        }

        ValidateRequiredLocales(sourceLocale, requiredLocales);

        SchemaVersion = schemaVersion;
        CatalogId = catalogId;
        SourceLocale = sourceLocale;
        RequiredLocales = requiredLocales.ToList();
        DefaultSyntaxProfile = defaultSyntaxProfile;
    }

    public int SchemaVersion { get; }

    public CatalogId CatalogId { get; }

    public Locale SourceLocale { get; private set; }

    public IReadOnlyList<Locale> RequiredLocales { get; private set; }

    public MessageSyntaxProfile DefaultSyntaxProfile { get; private set; }

    public IReadOnlyDictionary<EntryKey, CatalogEntry> Entries => _entries;

    public void AddEntry(CatalogEntry entry)
    {
        if (_entries.ContainsKey(entry.Key))
        {
            throw new DomainValidationException(
                EntryKeyRules.DuplicateKeyCode,
                $"An entry with key '{entry.Key.Value}' already exists.");
        }

        EnsureNoCaseInsensitiveCollision(entry.Key);
        ValidateEntryText(entry);
        ValidateTranslations(entry);

        _entries.Add(entry.Key, entry);
    }

    public void ReplaceEntry(CatalogEntry entry)
    {
        if (!_entries.ContainsKey(entry.Key))
        {
            throw new DomainValidationException(
                "entry.not_found",
                $"No entry with key '{entry.Key.Value}' exists.");
        }

        ValidateEntryText(entry);
        ValidateTranslations(entry);

        _entries[entry.Key] = entry;
    }

    public void RemoveEntry(EntryKey key)
    {
        if (!_entries.Remove(key))
        {
            throw new DomainValidationException(
                "entry.not_found",
                $"No entry with key '{key.Value}' exists.");
        }
    }

    public void ConfigureLocales(Locale sourceLocale, IReadOnlyList<Locale> requiredLocales)
    {
        if (requiredLocales.Count == 0)
        {
            throw new DomainValidationException(
                "catalog.required_locales.empty",
                "At least one required locale must be configured.");
        }

        ValidateRequiredLocales(sourceLocale, requiredLocales);

        SourceLocale = sourceLocale;
        RequiredLocales = requiredLocales.ToList();
    }

    public void SetDefaultSyntaxProfile(MessageSyntaxProfile defaultSyntaxProfile)
    {
        DefaultSyntaxProfile = defaultSyntaxProfile;
    }

    public Fingerprints.Fingerprint GetCurrentFingerprint(CatalogEntry entry) =>
        Fingerprints.FingerprintCanonicalization.ComputeForEntry(entry, SourceLocale, DefaultSyntaxProfile);

    public MessageSyntaxProfile GetEffectiveSyntaxProfile(CatalogEntry entry) =>
        entry.SyntaxProfileOverride ?? DefaultSyntaxProfile;

    public TranslationEffectiveStatus GetEffectiveStatus(CatalogEntry entry, Locale targetLocale)
    {
        entry.Translations.TryGetValue(targetLocale, out var translation);
        var currentFingerprint = GetCurrentFingerprint(entry);
        return TranslationStatusCalculator.GetEffectiveStatus(translation, currentFingerprint);
    }

    private void EnsureNoCaseInsensitiveCollision(EntryKey key)
    {
        var caseInsensitiveCollision = _entries.Keys.FirstOrDefault(
            existing => string.Equals(existing.Value, key.Value, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(existing.Value, key.Value, StringComparison.Ordinal));

        if (caseInsensitiveCollision is not null)
        {
            throw new DomainValidationException(
                EntryKeyRules.CaseInsensitiveCollisionCode,
                $"Entry key '{key.Value}' collides case-insensitively with '{caseInsensitiveCollision.Value}'.");
        }
    }

    private static void ValidateRequiredLocales(Locale sourceLocale, IReadOnlyList<Locale> requiredLocales)
    {
        if (requiredLocales.Any(locale => locale.Value.Equals(sourceLocale.Value, StringComparison.Ordinal)))
        {
            throw new DomainValidationException(
                "catalog.required_locales.includes_source",
                "The source locale cannot also be listed as a required target locale.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var locale in requiredLocales)
        {
            if (!seen.Add(locale.Value))
            {
                throw new DomainValidationException(
                    "catalog.required_locales.duplicate",
                    $"Required locale '{locale.Value}' is listed more than once.");
            }
        }
    }

    private static void ValidateEntryText(CatalogEntry entry)
    {
        if (!entry.AllowEmptyText && string.IsNullOrEmpty(entry.SourceText))
        {
            throw new DomainValidationException(
                "entry.source_text.empty",
                $"Entry '{entry.Key.Value}' cannot have empty source text unless allow-empty is enabled.");
        }
    }

    private static void ValidateTranslations(CatalogEntry entry)
    {
        foreach (var translation in entry.Translations.Values)
        {
            if (!entry.AllowEmptyText && string.IsNullOrEmpty(translation.Text))
            {
                throw new DomainValidationException(
                    "translation.text.empty",
                    $"Translation for entry '{entry.Key.Value}' cannot have empty text unless allow-empty is enabled.");
            }
        }
    }
}
