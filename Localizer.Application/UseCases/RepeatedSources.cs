using Localizer.Core.Catalogs;
using Localizer.Core.Fingerprints;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;

namespace Localizer.Application.UseCases;

/// <summary>
/// Entries repeat a source when their source text, ignoring newline style, and effective syntax
/// profile are equal. Derived from the catalog on demand and never stored.
/// </summary>
public static class RepeatedSources
{
    public static bool IsSameSource(Catalog catalog, CatalogEntry left, CatalogEntry right)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return GroupOf(catalog, left) == GroupOf(catalog, right);
    }

    /// <summary>
    /// How many entries share each entry's source, the entry itself included.
    /// </summary>
    public static IReadOnlyDictionary<EntryKey, int> CountByEntry(Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return catalog.Entries.Values
            .GroupBy(entry => GroupOf(catalog, entry))
            .SelectMany(group =>
            {
                var size = group.Count();
                return group.Select(entry => (entry.Key, Size: size));
            })
            .ToDictionary(item => item.Key, item => item.Size);
    }

    private static (string Text, MessageSyntaxProfile Syntax) GroupOf(Catalog catalog, CatalogEntry entry) =>
        (FingerprintCanonicalization.NormalizeNewlines(entry.SourceText), catalog.GetEffectiveSyntaxProfile(entry));
}
