namespace Localizer.Application.Project;

public interface IProjectFolderSettingsStore
{
    Task<ProjectFolderBinding?> GetAsync(string catalogId, CancellationToken cancellationToken = default);

    Task SaveAsync(ProjectFolderBinding binding, CancellationToken cancellationToken = default);
}
