namespace Localizer.Infrastructure.Persistence.Json;

public sealed class EntryConstraintsDto
{
    public int? MaxGraphemes { get; set; }

    public int? MaxUtf8Bytes { get; set; }

    public int? MaxLines { get; set; }

    public List<string>? RequiredTerms { get; set; }

    public List<string>? ForbiddenTerms { get; set; }
}
