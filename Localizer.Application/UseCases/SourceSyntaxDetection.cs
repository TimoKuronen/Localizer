using System.Text.RegularExpressions;
using Localizer.Core.Syntax;

namespace Localizer.Application.UseCases;

internal static partial class SourceSyntaxDetection
{
    public static MessageSyntaxProfile? DetectOverride(
        string sourceText,
        MessageSyntaxProfile? existingOverride)
    {
        if (existingOverride is not null)
        {
            return existingOverride;
        }

        return IndexedPlaceholderPattern().IsMatch(sourceText)
            ? MessageSyntaxProfile.Composite
            : null;
    }

    [GeneratedRegex(@"\{\d+\}", RegexOptions.CultureInvariant)]
    private static partial Regex IndexedPlaceholderPattern();
}
