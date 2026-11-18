using System.Text;
using System.Text.Json;

namespace Localizer.Desktop.Services;

public enum PickerKind
{
    Catalog,
    SourceStrings,
    ExportDirectory
}

/// <summary>
/// The folder each kind of file or folder picker last used, persisted so pickers reopen there.
/// Start locations are a convenience: unreadable or unwritable settings never fail a dialog.
/// </summary>
public sealed class PickerLocations
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public PickerLocations(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
    }

    /// <summary>
    /// The remembered folder for the kind, or null when none is stored or it no longer exists.
    /// </summary>
    public string? Get(PickerKind kind)
    {
        var folders = Load();
        return folders.TryGetValue(kind.ToString(), out var folder) && Directory.Exists(folder)
            ? folder
            : null;
    }

    public void Remember(PickerKind kind, string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        var folders = Load();
        if (folders.TryGetValue(kind.ToString(), out var current)
            && string.Equals(current, folder, StringComparison.Ordinal))
        {
            return;
        }

        folders[kind.ToString()] = folder;
        Save(folders);
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var bytes = File.ReadAllBytes(_settingsPath);
            var document = JsonSerializer.Deserialize<PickerLocationsDocument>(bytes, SerializerOptions);
            return document?.Folders is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(document.Folders, StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void Save(Dictionary<string, string> folders)
    {
        var json = JsonSerializer.Serialize(new PickerLocationsDocument { Folders = folders }, SerializerOptions) + "\n";
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
        var tempPath = _settingsPath + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Remembering a folder is best effort; the next dialog simply starts at the default folder.
        }
    }

    private sealed class PickerLocationsDocument
    {
        public Dictionary<string, string>? Folders { get; set; }
    }
}
