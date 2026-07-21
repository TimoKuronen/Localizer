using Localizer.Core.Errors;

namespace Localizer.Core.Identity;

public sealed record EntryKey
{
    private EntryKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static EntryKey Create(string value)
    {
        if (!TryCreate(value, out var key, out var error))
        {
            throw new DomainValidationException(error!.Code, error.Message);
        }

        return key!;
    }

    public static bool TryCreate(string value, out EntryKey? key, out DomainValidationException? error)
    {
        key = null;
        error = null;

        if (!EntryKeyRules.IsValid(value))
        {
            error = new DomainValidationException(
                EntryKeyRules.InvalidKeyCode,
                "Entry keys must contain only ASCII letters, digits, underscore, hyphen, or period.");
            return false;
        }

        key = new EntryKey(value);
        return true;
    }

    public override string ToString() => Value;
}
