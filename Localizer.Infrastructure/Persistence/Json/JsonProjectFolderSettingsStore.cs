using System.Text;
using System.Text.Json;
using Localizer.Application.Project;

namespace Localizer.Infrastructure.Persistence.Json;

public sealed class JsonProjectFolderSettingsStore : IProjectFolderSettingsStore
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public JsonProjectFolderSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
    }

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

    private async Task<ProjectFolderSettingsDocument> LoadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return new ProjectFolderSettingsDocument();
        }

        var bytes = await File.ReadAllBytesAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
        EnsureNoByteOrderMark(bytes);

        var document = JsonSerializer.Deserialize<ProjectFolderSettingsDocument>(bytes, _serializerOptions);
        return document ?? new ProjectFolderSettingsDocument();
    }

    private async Task SaveAllAsync(ProjectFolderSettingsDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new IOException($"Project-folder settings path '{_settingsPath}' does not include a directory.");
        }

        var json = JsonSerializer.Serialize(document, _serializerOptions);
        if (!json.EndsWith('\n'))
        {
            json += '\n';
        }

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
        var tempPath = _settingsPath + ".tmp";

        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);

            if (File.Exists(_settingsPath))
            {
                File.Replace(tempPath, _settingsPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, _settingsPath);
            }
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    private static void EnsureNoByteOrderMark(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            throw new InvalidDataException(
                "Project-folder settings JSON must be UTF-8 without a byte order mark.");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class ProjectFolderSettingsDocument
    {
        public List<ProjectFolderBinding> Bindings { get; set; } = [];
    }
}
