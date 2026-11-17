using Localizer.Application.Drafting;

namespace Localizer.Desktop.Services;

/// <summary>
/// The draft model choice: installed models, the saved preference, and an optional
/// environment override that takes precedence over the saved choice.
/// </summary>
public sealed class DraftModelSettings
{
    private readonly IDraftModelCatalog _catalog;
    private readonly IDraftModelPreference _preference;

    public DraftModelSettings(IDraftModelCatalog catalog, IDraftModelPreference preference, string? environmentModel)
    {
        _catalog = catalog;
        _preference = preference;
        EnvironmentModel = string.IsNullOrWhiteSpace(environmentModel) ? null : environmentModel.Trim();
    }

    public string? EnvironmentModel { get; }

    public bool IsFixedByEnvironment => EnvironmentModel is not null;

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken) =>
        _catalog.ListChatModelsAsync(cancellationToken);

    public Task<string?> GetSavedAsync(CancellationToken cancellationToken) =>
        _preference.GetAsync(cancellationToken);

    public Task SaveAsync(string? modelName, CancellationToken cancellationToken) =>
        _preference.SaveAsync(modelName, cancellationToken);
}
