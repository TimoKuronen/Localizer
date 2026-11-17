using System.Text;
using Localizer.Infrastructure.Persistence.Json;

namespace Localizer.Infrastructure.Tests.Persistence;

public sealed class JsonDraftModelPreferenceStoreTests
{
    [Test]
    public async Task GetAsync_ReturnsNullWhenFileIsMissing()
    {
        var store = new JsonDraftModelPreferenceStore(CreateTempSettingsPath());

        Assert.That(await store.GetAsync(), Is.Null);
    }

    [Test]
    public async Task SaveAsync_GetAsync_RoundTripsModelName()
    {
        var store = new JsonDraftModelPreferenceStore(CreateTempSettingsPath());

        await store.SaveAsync("qwen3.5:4b");

        Assert.That(await store.GetAsync(), Is.EqualTo("qwen3.5:4b"));
    }

    [Test]
    public async Task SaveAsync_NullClearsThePreference()
    {
        var store = new JsonDraftModelPreferenceStore(CreateTempSettingsPath());
        await store.SaveAsync("qwen3.5:4b");

        await store.SaveAsync(null);

        Assert.That(await store.GetAsync(), Is.Null);
    }

    [Test]
    public async Task SaveAsync_WritesUtf8WithoutByteOrderMarkAndTrailingNewline()
    {
        var path = CreateTempSettingsPath();

        await new JsonDraftModelPreferenceStore(path).SaveAsync("mistral:latest");

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.That(Encoding.UTF8.GetString(bytes), Does.EndWith("\n"));
    }

    [Test]
    public async Task GetAsync_RejectsByteOrderMark()
    {
        var path = CreateTempSettingsPath();
        await File.WriteAllBytesAsync(path, [0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}']);

        Assert.That(
            async () => await new JsonDraftModelPreferenceStore(path).GetAsync(),
            Throws.TypeOf<InvalidDataException>());
    }

    private static string CreateTempSettingsPath()
    {
        return Path.Combine(TempDirectory.Create("localizer-draft-model-tests"), "draft-model.json");
    }
}
