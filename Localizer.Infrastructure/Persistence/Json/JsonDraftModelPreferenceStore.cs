using System.Text;
using System.Text.Json;
using Localizer.Application.Drafting;

namespace Localizer.Infrastructure.Persistence.Json;

public sealed class JsonDraftModelPreferenceStore : IDraftModelPreference
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public JsonDraftModelPreferenceStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
    }

    public async Task<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            throw new InvalidDataException("Draft model settings JSON must be UTF-8 without a byte order mark.");
        }

        var document = JsonSerializer.Deserialize<DraftModelDocument>(bytes, _serializerOptions);
        return string.IsNullOrWhiteSpace(document?.ModelName) ? null : document.ModelName;
    }

    public async Task SaveAsync(string? modelName, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new IOException($"Draft model settings path '{_settingsPath}' does not include a directory.");
        }

        var document = new DraftModelDocument
        {
            ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName.Trim()
        };
        var json = JsonSerializer.Serialize(document, _serializerOptions) + "\n";
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
        var tempPath = _settingsPath + ".tmp";

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

    private sealed class DraftModelDocument
    {
        public string? ModelName { get; set; }
    }
}
