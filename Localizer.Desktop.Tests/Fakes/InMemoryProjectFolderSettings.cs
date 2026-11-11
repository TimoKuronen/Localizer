using Localizer.Application.Project;

namespace Localizer.Desktop.Tests.Fakes;

internal sealed class InMemoryProjectFolderSettings : IProjectFolderSettingsStore
{
    private readonly Dictionary<string, ProjectFolderBinding> _bindings = new(StringComparer.Ordinal);

    public void Put(ProjectFolderBinding binding) => _bindings[binding.CatalogId] = binding;

    public Task<ProjectFolderBinding?> GetAsync(
        string catalogId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_bindings.GetValueOrDefault(catalogId));

    public Task SaveAsync(ProjectFolderBinding binding, CancellationToken cancellationToken = default)
    {
        _bindings[binding.CatalogId] = binding;
        return Task.CompletedTask;
    }
}
