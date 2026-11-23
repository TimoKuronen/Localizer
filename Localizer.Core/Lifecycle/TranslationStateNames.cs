namespace Localizer.Core.Lifecycle;

public static class TranslationStateNames
{
    public const string Draft = "draft";
    public const string Approved = "approved";

    public static string ToName(TranslationState state) =>
        state switch
        {
            TranslationState.Draft => Draft,
            TranslationState.Approved => Approved,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

    public static TranslationState FromName(string value) =>
        value switch
        {
            Draft => TranslationState.Draft,
            Approved => TranslationState.Approved,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported translation state.")
        };
}
