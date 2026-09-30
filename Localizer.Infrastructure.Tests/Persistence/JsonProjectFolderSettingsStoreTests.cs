using System.Text;
using Localizer.Application.Project;
using Localizer.Infrastructure.Persistence.Json;

namespace Localizer.Infrastructure.Tests.Persistence;

public sealed class JsonProjectFolderSettingsStoreTests
{
    [Test]
    public async Task SaveAsync_GetAsync_RoundTripsBinding()
    {
        var path = CreateTempSettingsPath();
        var store = new JsonProjectFolderSettingsStore(path);
        var binding = new ProjectFolderBinding
        {
            CatalogId = "sample-game",
            FolderPath = Path.Combine(Path.GetTempPath(), "unity-project"),
            ImportFileName = "UI.csv",
            ExportFileName = "UI.csv"
        };

        await store.SaveAsync(binding);
        var restored = await store.GetAsync("sample-game");

        Assert.That(restored, Is.Not.Null);
        Assert.That(restored!.CatalogId, Is.EqualTo(binding.CatalogId));
        Assert.That(restored.FolderPath, Is.EqualTo(binding.FolderPath));
        Assert.That(restored.ImportFileName, Is.EqualTo(binding.ImportFileName));
        Assert.That(restored.ExportFileName, Is.EqualTo(binding.ExportFileName));
    }

    [Test]
    public async Task SaveAsync_WritesUtf8WithoutByteOrderMarkAndTrailingNewline()
    {
        var path = CreateTempSettingsPath();
        var store = new JsonProjectFolderSettingsStore(path);

        await store.SaveAsync(new ProjectFolderBinding
        {
            CatalogId = "demo",
            FolderPath = @"D:\Games\Demo",
            ImportFileName = "import.csv",
            ExportFileName = "export.csv"
        });

        var bytes = await File.ReadAllBytesAsync(path);

        Assert.That(bytes.Length, Is.GreaterThan(0));
        Assert.That(bytes[0], Is.Not.EqualTo(0xEF));
        Assert.That(bytes[^1], Is.EqualTo((byte)'\n'));
    }

    [Test]
    public async Task SaveAsync_ReplacesExistingFileAtomically()
    {
        var path = CreateTempSettingsPath();
        var store = new JsonProjectFolderSettingsStore(path);

        await store.SaveAsync(new ProjectFolderBinding
        {
            CatalogId = "demo",
            FolderPath = @"D:\Games\Demo",
            ImportFileName = "old.csv",
            ExportFileName = "old.csv"
        });

        await store.SaveAsync(new ProjectFolderBinding
        {
            CatalogId = "demo",
            FolderPath = @"D:\Games\Demo",
            ImportFileName = "new.csv",
            ExportFileName = "new.csv"
        });

        var restored = await store.GetAsync("demo");
        Assert.That(restored!.ImportFileName, Is.EqualTo("new.csv"));
        Assert.That(File.Exists(path + ".tmp"), Is.False);
    }

    [Test]
    public async Task GetAsync_ReturnsNullWhenCatalogIdMissing()
    {
        var path = CreateTempSettingsPath();
        var store = new JsonProjectFolderSettingsStore(path);

        await store.SaveAsync(new ProjectFolderBinding
        {
            CatalogId = "demo",
            FolderPath = @"D:\Games\Demo",
            ImportFileName = "UI.csv",
            ExportFileName = "UI.csv"
        });

        var missing = await store.GetAsync("other");
        Assert.That(missing, Is.Null);
    }

    [Test]
    public async Task Load_RejectsByteOrderMark()
    {
        var path = CreateTempSettingsPath();
        var json = """
            {"bindings":[]}
            """;
        var jsonBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
        var bytes = new byte[jsonBytes.Length + 3];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        Array.Copy(jsonBytes, 0, bytes, 3, jsonBytes.Length);
        await File.WriteAllBytesAsync(path, bytes);

        var store = new JsonProjectFolderSettingsStore(path);
        Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync("demo"));
    }

    private static string CreateTempSettingsPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "localizer-project-folder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "project-folders.json");
    }
}
