namespace Localizer.Application.Drafting;

/// <summary>
/// Persisted model choice for drafting. A null model means the provider decides, which
/// succeeds only when a single model is available.
/// </summary>
public interface IDraftModelPreference
{
    Task<string?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(string? modelName, CancellationToken cancellationToken = default);
}
