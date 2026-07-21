using Localizer.Core.Errors;

namespace Localizer.Core.Identity;

public sealed record Locale
{
    private Locale(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Locale Create(string value)
    {
        if (!TryCreate(value, out var locale, out var error))
        {
            throw new DomainValidationException(error!.Code, error.Message);
        }

        return locale!;
    }

    public static bool TryCreate(string value, out Locale? locale, out DomainValidationException? error)
    {
        locale = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = new DomainValidationException(
                "locale.invalid",
                "Locale identifiers must be non-empty BCP 47 tags.");
            return false;
        }

        var normalized = Normalize(value);
        if (string.IsNullOrEmpty(normalized))
        {
            error = new DomainValidationException(
                "locale.invalid",
                "Locale identifiers must be non-empty BCP 47 tags.");
            return false;
        }

        locale = new Locale(normalized);
        return true;
    }

    internal static string Normalize(string value)
    {
        var trimmed = value.Trim().Replace('_', '-');
        var parts = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        parts[0] = parts[0].ToLowerInvariant();

        for (var index = 1; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part.Length == 4 && part.All(char.IsLetter))
            {
                parts[index] = char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
            }
            else if (part.Length == 2 && part.All(char.IsLetter))
            {
                parts[index] = part.ToUpperInvariant();
            }
            else
            {
                parts[index] = part.ToLowerInvariant();
            }
        }

        return string.Join('-', parts);
    }

    public override string ToString() => Value;
}
