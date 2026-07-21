using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;
using Localizer.Core.Errors;
using Localizer.Core.Fingerprints;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;
using Localizer.Core.Syntax;

namespace Localizer.Infrastructure.Persistence.Json;

public sealed class CatalogJsonMapper
{
    public const int SupportedSchemaVersion = 1;

    public Catalog ToDomain(CatalogDocumentDto document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.UnsupportedVersion,
                $"Catalog schema version {document.SchemaVersion} is not supported. Expected version {SupportedSchemaVersion}.");
        }

        try
        {
            var catalog = new Catalog(
                document.SchemaVersion,
                CatalogId.Create(document.CatalogId),
                Locale.Create(document.SourceLocale),
                document.RequiredLocales.Select(Locale.Create).ToList(),
                MessageSyntaxProfileNames.FromName(document.DefaultSyntaxProfile));

            foreach (var entryDto in document.Entries)
            {
                catalog.AddEntry(MapEntry(entryDto));
            }

            return catalog;
        }
        catch (CatalogPersistenceException)
        {
            throw;
        }
        catch (DomainValidationException exception)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.MapFailed,
                exception.Message,
                exception);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.MapFailed,
                exception.Message,
                exception);
        }
    }

    public CatalogDocumentDto ToDocument(Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return new CatalogDocumentDto
        {
            SchemaVersion = catalog.SchemaVersion,
            CatalogId = catalog.CatalogId.Value,
            SourceLocale = catalog.SourceLocale.Value,
            RequiredLocales = catalog.RequiredLocales.Select(locale => locale.Value).ToList(),
            DefaultSyntaxProfile = MessageSyntaxProfileNames.ToName(catalog.DefaultSyntaxProfile),
            Entries = catalog.Entries.Values
                .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
                .Select(MapEntry)
                .ToList()
        };
    }

    private static CatalogEntry MapEntry(CatalogEntryDto entryDto)
    {
        MessageSyntaxProfile? syntaxProfileOverride = null;
        if (!string.IsNullOrWhiteSpace(entryDto.SyntaxProfile))
        {
            syntaxProfileOverride = MessageSyntaxProfileNames.FromName(entryDto.SyntaxProfile);
        }

        var translations = new Dictionary<Locale, Translation>(entryDto.Translations?.Count ?? 0);
        if (entryDto.Translations is not null)
        {
            foreach (var pair in entryDto.Translations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                translations[Locale.Create(pair.Key)] = MapTranslation(pair.Value);
            }
        }

        return new CatalogEntry
        {
            Key = EntryKey.Create(entryDto.Key),
            Category = entryDto.Category,
            SourceText = entryDto.SourceText,
            AllowEmptyText = entryDto.AllowEmptyText,
            DeveloperNotes = entryDto.DeveloperNotes,
            Context = MapContext(entryDto.Context),
            Constraints = MapConstraints(entryDto.Constraints),
            SyntaxProfileOverride = syntaxProfileOverride,
            ExternalIds = MapExternalIds(entryDto.ExternalIds),
            Translations = translations
        };
    }

    private static CatalogEntryDto MapEntry(CatalogEntry entry)
    {
        var entryDto = new CatalogEntryDto
        {
            Key = entry.Key.Value,
            Category = entry.Category,
            SourceText = entry.SourceText,
            AllowEmptyText = entry.AllowEmptyText,
            DeveloperNotes = entry.DeveloperNotes,
            Context = MapContext(entry.Context.Values),
            Constraints = MapConstraints(entry.Constraints),
            SyntaxProfile = entry.SyntaxProfileOverride is null
                ? null
                : MessageSyntaxProfileNames.ToName(entry.SyntaxProfileOverride.Value),
            ExternalIds = MapExternalIds(entry.ExternalIds.Values),
            Translations = entry.Translations.Count == 0
                ? null
                : entry.Translations
                    .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                    .ToDictionary(
                        pair => pair.Key.Value,
                        pair => MapTranslation(pair.Value),
                        StringComparer.Ordinal)
        };

        return entryDto;
    }

    private static Translation MapTranslation(TranslationDto translationDto)
    {
        var provenance = new TranslationProvenance
        {
            Origin = TranslationOriginNames.FromName(translationDto.Origin),
            ProviderName = translationDto.Provenance?.ProviderName,
            ModelName = translationDto.Provenance?.ModelName,
            GeneratedAtUtc = translationDto.Provenance?.GeneratedAtUtc
        };

        return new Translation
        {
            Text = translationDto.Text,
            State = TranslationStateNames.FromName(translationDto.State),
            BasedOnFingerprint = Fingerprint.Parse(translationDto.BasedOnFingerprint),
            Provenance = provenance
        };
    }

    private static TranslationDto MapTranslation(Translation translation)
    {
        var translationDto = new TranslationDto
        {
            Text = translation.Text,
            State = TranslationStateNames.ToName(translation.State),
            BasedOnFingerprint = translation.BasedOnFingerprint.Value,
            Origin = TranslationOriginNames.ToName(translation.Provenance.Origin)
        };

        if (translation.Provenance.ProviderName is not null
            || translation.Provenance.ModelName is not null
            || translation.Provenance.GeneratedAtUtc is not null)
        {
            translationDto.Provenance = new TranslationProvenanceDto
            {
                ProviderName = translation.Provenance.ProviderName,
                ModelName = translation.Provenance.ModelName,
                GeneratedAtUtc = translation.Provenance.GeneratedAtUtc
            };
        }

        return translationDto;
    }

    private static TranslationContext MapContext(Dictionary<string, string>? values) =>
        new()
        {
            Values = values is null || values.Count == 0
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : values
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
        };

    private static Dictionary<string, string>? MapContext(IReadOnlyDictionary<string, string> values) =>
        values.Count == 0
            ? null
            : values
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static EntryConstraints MapConstraints(EntryConstraintsDto? constraintsDto)
    {
        if (constraintsDto is null)
        {
            return new EntryConstraints();
        }

        return new EntryConstraints
        {
            MaxGraphemes = constraintsDto.MaxGraphemes,
            MaxUtf8Bytes = constraintsDto.MaxUtf8Bytes,
            MaxLines = constraintsDto.MaxLines,
            RequiredTerms = constraintsDto.RequiredTerms ?? [],
            ForbiddenTerms = constraintsDto.ForbiddenTerms ?? []
        };
    }

    private static EntryConstraintsDto? MapConstraints(EntryConstraints constraints)
    {
        if (constraints.MaxGraphemes is null
            && constraints.MaxUtf8Bytes is null
            && constraints.MaxLines is null
            && constraints.RequiredTerms.Count == 0
            && constraints.ForbiddenTerms.Count == 0)
        {
            return null;
        }

        return new EntryConstraintsDto
        {
            MaxGraphemes = constraints.MaxGraphemes,
            MaxUtf8Bytes = constraints.MaxUtf8Bytes,
            MaxLines = constraints.MaxLines,
            RequiredTerms = constraints.RequiredTerms.Count == 0
                ? null
                : constraints.RequiredTerms.OrderBy(term => term, StringComparer.Ordinal).ToList(),
            ForbiddenTerms = constraints.ForbiddenTerms.Count == 0
                ? null
                : constraints.ForbiddenTerms.OrderBy(term => term, StringComparer.Ordinal).ToList()
        };
    }

    private static ExternalIds MapExternalIds(Dictionary<string, string>? values) =>
        new()
        {
            Values = values is null || values.Count == 0
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : values
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
        };

    private static Dictionary<string, string>? MapExternalIds(IReadOnlyDictionary<string, string> values) =>
        values.Count == 0
            ? null
            : values
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
}
