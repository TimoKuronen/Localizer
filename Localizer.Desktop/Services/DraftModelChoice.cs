namespace Localizer.Desktop.Services;

/// <summary>
/// Result of the model dialog. A null model name selects automatic resolution.
/// </summary>
public sealed record DraftModelChoice(string? ModelName);
