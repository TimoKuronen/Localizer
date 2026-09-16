using Localizer.Application.Import;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;

namespace Localizer.Application.UseCases;

public sealed record MergeUnityCsvImportOutcome
{
    public required bool Succeeded { get; init; }

    public required int AddedCount { get; init; }

    public required int UpdatedCount { get; init; }

    public required int UnchangedCount { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed partial class MergeUnityCsvImportUseCase
{
    public MergeUnityCsvImportOutcome Execute(Catalog catalog, UnityCsvDocument document)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(document);

        if (!string.Equals(document.SourceLocale, catalog.SourceLocale.Value, StringComparison.OrdinalIgnoreCase))
        {
            return new MergeUnityCsvImportOutcome
            {
                Succeeded = false,
                AddedCount = 0,
                UpdatedCount = 0,
                UnchangedCount = 0,
                ErrorCode = UseCaseErrorCodes.ImportLocaleMismatch,
                ErrorMessage =
                    $"CSV source locale '{document.SourceLocale}' does not match catalog source locale '{catalog.SourceLocale.Value}'."
            };
        }

        foreach (var targetLocale in document.TargetLocales)
        {
            if (!catalog.RequiredLocales.Any(locale =>
                    locale.Value.Equals(targetLocale, StringComparison.OrdinalIgnoreCase)))
            {
                return new MergeUnityCsvImportOutcome
                {
                    Succeeded = false,
                    AddedCount = 0,
                    UpdatedCount = 0,
                    UnchangedCount = 0,
                    ErrorCode = UseCaseErrorCodes.ImportLocaleMismatch,
                    ErrorMessage =
                        $"CSV target locale '{targetLocale}' is not configured as a required locale for this catalog."
                };
            }
        }

        var added = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var row in document.Rows)
        {
            var key = EntryKey.Create(row.Key);

            if (!catalog.Entries.TryGetValue(key, out var existing))
            {
                var entry = CreateEntryFromRow(row);
                catalog.AddEntry(entry);
                added++;
                continue;
            }

            var mergedExternalIds = MergeExternalIds(existing.ExternalIds, row.UnityId);
            var syntaxOverride = DetectSyntaxProfileOverride(row.SourceText, existing.SyntaxProfileOverride);
            var sourceChanged = !string.Equals(existing.SourceText, row.SourceText, StringComparison.Ordinal);
            var unityIdChanged = !string.Equals(
                existing.ExternalIds.Values.GetValueOrDefault(UnityCsvBridgeConstants.UnityExternalIdNamespace),
                row.UnityId,
                StringComparison.Ordinal);
            var syntaxChanged = existing.SyntaxProfileOverride != syntaxOverride;

            if (!sourceChanged && !unityIdChanged && !syntaxChanged)
            {
                unchanged++;
                continue;
            }

            catalog.ReplaceEntry(existing with
            {
                SourceText = row.SourceText,
                ExternalIds = mergedExternalIds,
                SyntaxProfileOverride = syntaxOverride
            });
            updated++;
        }

        return new MergeUnityCsvImportOutcome
        {
            Succeeded = true,
            AddedCount = added,
            UpdatedCount = updated,
            UnchangedCount = unchanged
        };
    }

    private static CatalogEntry CreateEntryFromRow(UnityCsvRow row) =>
        new()
        {
            Key = EntryKey.Create(row.Key),
            SourceText = row.SourceText,
            Category = InferCategory(row.Key),
            ExternalIds = MergeExternalIds(new ExternalIds(), row.UnityId),
            SyntaxProfileOverride = DetectSyntaxProfileOverride(row.SourceText, syntaxProfileOverride: null),
            Translations = new Dictionary<Locale, Translation>()
        };

    private static ExternalIds MergeExternalIds(ExternalIds existing, string unityId)
    {
        if (string.IsNullOrWhiteSpace(unityId))
        {
            return existing;
        }

        var values = new Dictionary<string, string>(existing.Values, StringComparer.Ordinal)
        {
            [UnityCsvBridgeConstants.UnityExternalIdNamespace] = unityId
        };

        return new ExternalIds { Values = values };
    }

    private static string? InferCategory(string key)
    {
        var separatorIndex = key.IndexOf('.');
        return separatorIndex > 0 ? key[..separatorIndex] : null;
    }

    private static MessageSyntaxProfile? DetectSyntaxProfileOverride(
        string sourceText,
        MessageSyntaxProfile? syntaxProfileOverride)
    {
        if (syntaxProfileOverride is not null)
        {
            return syntaxProfileOverride;
        }

        return IndexedPlaceholderPattern().IsMatch(sourceText)
            ? MessageSyntaxProfile.Composite
            : null;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\{\d+\}", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex IndexedPlaceholderPattern();
}

