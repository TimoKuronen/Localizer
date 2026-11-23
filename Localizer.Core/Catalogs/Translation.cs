using Localizer.Core.Lifecycle;

namespace Localizer.Core.Catalogs;

public sealed record Translation
{
    public required string Text { get; init; }
    public required TranslationState State { get; init; }
    public required Fingerprints.Fingerprint BasedOnFingerprint { get; init; }
    public required TranslationProvenance Provenance { get; init; }
}
