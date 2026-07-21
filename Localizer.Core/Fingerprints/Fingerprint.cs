using System.Security.Cryptography;
using System.Text;
using Localizer.Core.Errors;

namespace Localizer.Core.Fingerprints;

public sealed record Fingerprint
{
    public const string AlgorithmPrefix = "sha256:";

    private Fingerprint(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Fingerprint Create(string canonicalInput)
    {
        var bytes = Encoding.UTF8.GetBytes(canonicalInput);
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return new Fingerprint(AlgorithmPrefix + hex);
    }

    public static Fingerprint Parse(string value)
    {
        if (!TryParse(value, out var fingerprint, out var error))
        {
            throw new DomainValidationException(error!.Code, error.Message);
        }

        return fingerprint!;
    }

    public static bool TryParse(string value, out Fingerprint? fingerprint, out DomainValidationException? error)
    {
        fingerprint = null;
        error = null;

        if (!value.StartsWith(AlgorithmPrefix, StringComparison.Ordinal))
        {
            error = new DomainValidationException(
                "fingerprint.invalid",
                "Fingerprints must use the sha256 prefix.");
            return false;
        }

        var hex = value[AlgorithmPrefix.Length..];
        if (hex.Length != 64 || !hex.All(static c => Uri.IsHexDigit(c)))
        {
            error = new DomainValidationException(
                "fingerprint.invalid",
                "Fingerprints must contain a 64-character lowercase hexadecimal digest.");
            return false;
        }

        if (hex != hex.ToLowerInvariant())
        {
            error = new DomainValidationException(
                "fingerprint.invalid",
                "Fingerprints must use lowercase hexadecimal digits.");
            return false;
        }

        fingerprint = new Fingerprint(value);
        return true;
    }

    public override string ToString() => Value;
}
