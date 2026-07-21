using Localizer.Core.Catalogs;
using Localizer.Core.Fingerprints;

namespace Localizer.Core.Lifecycle;

public static class TranslationStatusCalculator
{
    public static TranslationEffectiveStatus GetEffectiveStatus(
        Translation? translation,
        Fingerprint currentFingerprint)
    {
        if (translation is null)
        {
            return TranslationEffectiveStatus.Missing;
        }

        if (!translation.BasedOnFingerprint.Equals(currentFingerprint))
        {
            return TranslationEffectiveStatus.Stale;
        }

        return translation.State switch
        {
            TranslationState.Draft => TranslationEffectiveStatus.Draft,
            TranslationState.Approved => TranslationEffectiveStatus.Approved,
            _ => throw new InvalidOperationException("Unsupported translation state.")
        };
    }
}
