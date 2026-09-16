using System.Text.Json;
using Localizer.Application.Project;

namespace Localizer.Desktop.Services;

public sealed class JsonProjectFolderSettingsStore : IProjectFolderSettingsStore
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<ProjectFolderBinding?> GetAsync(
        string catalogId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogId);

        var settings = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        return settings.Bindings.FirstOrDefault(binding =>
            string.Equals(binding.CatalogId, catalogId, StringComparison.Ordinal));
    }

    public async Task SaveAsync(ProjectFolderBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.CatalogId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.FolderPath);

        var settings = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        settings.Bindings.RemoveAll(existing =>
            string.Equals(existing.CatalogId, binding.CatalogId, StringComparison.Ordinal));
        settings.Bindings.Add(binding);
        settings.Bindings.Sort((left, right) =>
            string.Compare(left.CatalogId, right.CatalogId, StringComparison.Ordinal));

        await SaveAllAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    private static string SettingsPath =>
        Path.Combine(WorkspaceStorage.EnsureDirectory(), "project-folders.json");

    private async Task<ProjectFolderSettingsDocument> LoadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SettingsPath))
        {
            return new ProjectFolderSettingsDocument();
        }

        await using var stream = File.OpenRead(SettingsPath);
        var document = await JsonSerializer
            .DeserializeAsync<ProjectFolderSettingsDocument>(stream, _serializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return document ?? new ProjectFolderSettingsDocument();
    }

    private async Task SaveAllAsync(ProjectFolderSettingsDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, document, _serializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class ProjectFolderSettingsDocument
    {
        public List<ProjectFolderBinding> Bindings { get; set; } = [];
    }
}
