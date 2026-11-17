using Avalonia.Controls;
using Localizer.Application.Project;
using Localizer.Application.UseCases;

namespace Localizer.Desktop.Services;

public interface IUiDialogs
{
    void Attach(Window owner);

    Task<string?> PickOpenCatalogPathAsync(CancellationToken cancellationToken = default);

    Task<string?> PickSaveCatalogPathAsync(
        string? suggestedFileName,
        CancellationToken cancellationToken = default);

    Task<string?> PickExportDirectoryAsync(CancellationToken cancellationToken = default);

    Task<ProjectFolderBinding?> PromptProjectFolderAsync(
        string folderPath,
        ProjectFolderBinding? existing = null);

    Task<CreateCatalogRequest?> PromptCreateCatalogAsync();

    Task<AddCatalogEntryRequest?> PromptAddEntryAsync();

    Task<UnsavedChangesChoice> PromptUnsavedChangesAsync(string message);

    Task<ConfigureCatalogSettingsRequest?> PromptCatalogSettingsAsync(ConfigureCatalogSettingsRequest current);

    Task<DraftModelChoice?> PromptDraftModelAsync(IReadOnlyList<string> models, string? savedModel);

    Task ShowMessageAsync(string title, string message);
}
