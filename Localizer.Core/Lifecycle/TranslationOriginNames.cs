namespace Localizer.Core.Lifecycle;

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
