namespace Localizer.Core.Identity;

public static class EntryKeyRules
{
    public const string InvalidKeyCode = "entry.key.invalid";
    public const string DuplicateKeyCode = "entry.key.duplicate";
    public const string CaseInsensitiveCollisionCode = "entry.key.case_insensitive_collision";

    private static readonly System.Text.RegularExpressions.Regex ValidPattern =
        new("^[A-Za-z0-9._-]+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static bool IsValid(string value) =>
        !string.IsNullOrWhiteSpace(value) && ValidPattern.IsMatch(value);
}
