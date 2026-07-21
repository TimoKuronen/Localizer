namespace Localizer.Core.Lifecycle;

public enum TranslationState
{
    Draft,
    Approved
}

public enum TranslationEffectiveStatus
{
    Missing,
    Stale,
    Draft,
    Approved
}

public enum TranslationOrigin
{
    Human,
    Model,
    Import
}

public static class TranslationOriginNames
{
    public const string Human = "human";
    public const string Model = "model";
    public const string Import = "import";

    public static string ToName(TranslationOrigin origin) =>
        origin switch
        {
            TranslationOrigin.Human => Human,
            TranslationOrigin.Model => Model,
            TranslationOrigin.Import => Import,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
        };

    public static TranslationOrigin FromName(string value) =>
        value switch
        {
            Human => TranslationOrigin.Human,
            Model => TranslationOrigin.Model,
            Import => TranslationOrigin.Import,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported translation origin.")
        };
}

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
