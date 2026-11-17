namespace Localizer.Application.Drafting;

/// <summary>
/// Lists the models a draft provider can currently draft with.
/// </summary>
public interface IDraftModelCatalog
{
    Task<IReadOnlyList<string>> ListChatModelsAsync(CancellationToken cancellationToken = default);
}
