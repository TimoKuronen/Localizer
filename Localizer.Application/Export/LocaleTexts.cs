using Localizer.Core.Identity;

namespace Localizer.Application.Export;

public sealed record LocaleTexts
{
    public required Locale Locale { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> Texts { get; init; }
}
