namespace Localizer.Infrastructure.Persistence.Json;

public sealed class TranslationDto
{
    public string Text { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string BasedOnFingerprint { get; set; } = string.Empty;

    public string Origin { get; set; } = string.Empty;

    public TranslationProvenanceDto? Provenance { get; set; }
}
