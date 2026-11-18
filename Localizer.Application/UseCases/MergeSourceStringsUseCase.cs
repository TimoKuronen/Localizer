using Localizer.Application.Import;
using Localizer.Core.Catalogs;
using Localizer.Core.Errors;
using Localizer.Core.Identity;

namespace Localizer.Application.UseCases;

public sealed record SourceStringsImportOutcome
{
    public required bool Succeeded { get; init; }

    public required int AddedCount { get; init; }

    public required int UpdatedCount { get; init; }

    public required int UnchangedCount { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static SourceStringsImportOutcome Failure(string code, string message) =>
        new()
        {
            Succeeded = false,
            AddedCount = 0,
            UpdatedCount = 0,
            UnchangedCount = 0,
            ErrorCode = code,
            ErrorMessage = message
        };
}

/// <summary>
/// Adds new keys and updates source-side fields of existing keys. Translations are never touched:
/// a changed source input makes them Stale through fingerprints, and Approved text is kept.
/// Optional fields absent from a row leave the existing value unchanged. A row that fails catalog
/// validation aborts the merge and restores every change made before it.
/// </summary>
public sealed class MergeSourceStringsUseCase
{
    public SourceStringsImportOutcome Execute(Catalog catalog, SourceStringsDocument document)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(document);

        if (!string.Equals(document.SourceLocale, catalog.SourceLocale.Value, StringComparison.OrdinalIgnoreCase))
        {
            return SourceStringsImportOutcome.Failure(
                UseCaseErrorCodes.ImportLocaleMismatch,
                $"Source file locale '{document.SourceLocale}' does not match catalog source locale '{catalog.SourceLocale.Value}'.");
        }

        var added = 0;
        var updated = 0;
        var unchanged = 0;
        var undo = new List<Action>();

        try
        {
            foreach (var row in document.Rows)
            {
                var key = EntryKey.Create(row.Key);

                if (!catalog.Entries.TryGetValue(key, out var existing))
                {
                    catalog.AddEntry(CreateEntry(key, row));
                    undo.Add(() => catalog.RemoveEntry(key));
                    added++;
                    continue;
                }

                var merged = MergeInto(existing, row);
                if (IsSameSourceSide(existing, merged))
                {
                    unchanged++;
                    continue;
                }

                catalog.ReplaceEntry(merged);
                undo.Add(() => catalog.ReplaceEntry(existing));
                updated++;
            }
        }
        catch (DomainValidationException)
        {
            for (var index = undo.Count - 1; index >= 0; index--)
            {
                undo[index]();
            }

            throw;
        }

        return new SourceStringsImportOutcome
        {
            Succeeded = true,
            AddedCount = added,
            UpdatedCount = updated,
            UnchangedCount = unchanged
        };
    }

    private static CatalogEntry CreateEntry(EntryKey key, SourceStringRow row) =>
        MergeInto(
            new CatalogEntry
            {
                Key = key,
                SourceText = row.SourceText,
                Translations = new Dictionary<Locale, Translation>()
            },
            row);

    private static CatalogEntry MergeInto(CatalogEntry existing, SourceStringRow row) =>
        existing with
        {
            SourceText = row.SourceText,
            Category = row.Category ?? existing.Category,
            AllowEmptyText = row.AllowEmptyText ?? existing.AllowEmptyText,
            DeveloperNotes = row.DeveloperNotes ?? existing.DeveloperNotes,
            Context = row.Context is null ? existing.Context : new TranslationContext { Values = row.Context },
            Constraints = row.Constraints ?? existing.Constraints,
            SyntaxProfileOverride = SourceSyntaxDetection.DetectOverride(
                row.SourceText,
                row.SyntaxProfile ?? existing.SyntaxProfileOverride),
            ExternalIds = MergeExternalIds(existing.ExternalIds, row.ExternalIds)
        };

    private static ExternalIds MergeExternalIds(ExternalIds existing, IReadOnlyDictionary<string, string>? incoming)
    {
        if (incoming is null || incoming.Count == 0)
        {
            return existing;
        }

        var values = new Dictionary<string, string>(existing.Values, StringComparer.Ordinal);
        foreach (var (namespaceName, value) in incoming)
        {
            values[namespaceName] = value;
        }

        return new ExternalIds { Values = values };
    }

    private static bool IsSameSourceSide(CatalogEntry left, CatalogEntry right) =>
        string.Equals(left.SourceText, right.SourceText, StringComparison.Ordinal)
        && string.Equals(left.Category, right.Category, StringComparison.Ordinal)
        && left.AllowEmptyText == right.AllowEmptyText
        && string.Equals(left.DeveloperNotes, right.DeveloperNotes, StringComparison.Ordinal)
        && left.SyntaxProfileOverride == right.SyntaxProfileOverride
        && SameMap(left.Context.Values, right.Context.Values)
        && SameMap(left.ExternalIds.Values, right.ExternalIds.Values)
        && SameConstraints(left.Constraints, right.Constraints);

    private static bool SameMap(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count
        && left.All(pair => right.TryGetValue(pair.Key, out var value)
                            && string.Equals(pair.Value, value, StringComparison.Ordinal));

    private static bool SameConstraints(EntryConstraints left, EntryConstraints right) =>
        left.MaxGraphemes == right.MaxGraphemes
        && left.MaxUtf8Bytes == right.MaxUtf8Bytes
        && left.MaxLines == right.MaxLines
        && left.RequiredTerms.SequenceEqual(right.RequiredTerms, StringComparer.Ordinal)
        && left.ForbiddenTerms.SequenceEqual(right.ForbiddenTerms, StringComparer.Ordinal);
}
