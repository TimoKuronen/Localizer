namespace Localizer.Infrastructure.Persistence.Json;

public sealed class TranslationProvenanceDto
{
    public string? ProviderName { get; set; }

    public string? ModelName { get; set; }

    public DateTimeOffset? GeneratedAtUtc { get; set; }
}
