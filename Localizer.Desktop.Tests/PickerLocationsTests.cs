using Localizer.Desktop.Services;

namespace Localizer.Desktop.Tests;

public sealed class PickerLocationsTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void CreateDirectory() =>
        _directory = Directory.CreateTempSubdirectory("localizer-picker-locations-tests-").FullName;

    [TearDown]
    public void DeleteDirectory() =>
        Directory.Delete(_directory, recursive: true);

    [Test]
    public void Get_ReturnsNullWhenNothingWasRemembered()
    {
        var locations = new PickerLocations(Path.Combine(_directory, "locations.json"));

        Assert.That(locations.Get(PickerKind.Catalog), Is.Null);
    }

    [Test]
    public void Remember_PersistsPerKindAcrossInstances()
    {
        var settings = Path.Combine(_directory, "locations.json");
        var catalogFolder = Directory.CreateDirectory(Path.Combine(_directory, "catalogs")).FullName;
        var importFolder = Directory.CreateDirectory(Path.Combine(_directory, "imports")).FullName;

        var first = new PickerLocations(settings);
        first.Remember(PickerKind.Catalog, catalogFolder);
        first.Remember(PickerKind.SourceStrings, importFolder);

        var second = new PickerLocations(settings);
        Assert.Multiple(() =>
        {
            Assert.That(second.Get(PickerKind.Catalog), Is.EqualTo(catalogFolder));
            Assert.That(second.Get(PickerKind.SourceStrings), Is.EqualTo(importFolder));
            Assert.That(second.Get(PickerKind.ExportDirectory), Is.Null);
        });
    }

    [Test]
    public void Remember_OverwritesThePreviousFolderForTheSameKind()
    {
        var settings = Path.Combine(_directory, "locations.json");
        var older = Directory.CreateDirectory(Path.Combine(_directory, "older")).FullName;
        var newer = Directory.CreateDirectory(Path.Combine(_directory, "newer")).FullName;
        var locations = new PickerLocations(settings);

        locations.Remember(PickerKind.Catalog, older);
        locations.Remember(PickerKind.Catalog, newer);

        Assert.That(new PickerLocations(settings).Get(PickerKind.Catalog), Is.EqualTo(newer));
    }

    [Test]
    public void Get_ReturnsNullWhenTheRememberedFolderNoLongerExists()
    {
        var settings = Path.Combine(_directory, "locations.json");
        var folder = Directory.CreateDirectory(Path.Combine(_directory, "gone")).FullName;
        var locations = new PickerLocations(settings);
        locations.Remember(PickerKind.Catalog, folder);

        Directory.Delete(folder);

        Assert.That(locations.Get(PickerKind.Catalog), Is.Null);
    }

    [Test]
    public void Remember_IgnoresMissingFoldersAndBlankValues()
    {
        var settings = Path.Combine(_directory, "locations.json");
        var locations = new PickerLocations(settings);

        locations.Remember(PickerKind.Catalog, Path.Combine(_directory, "does-not-exist"));
        locations.Remember(PickerKind.Catalog, null);
        locations.Remember(PickerKind.Catalog, "  ");

        Assert.Multiple(() =>
        {
            Assert.That(locations.Get(PickerKind.Catalog), Is.Null);
            Assert.That(File.Exists(settings), Is.False);
        });
    }

    [Test]
    public void Get_FallsBackToNullWhenTheSettingsFileIsCorrupt()
    {
        var settings = Path.Combine(_directory, "locations.json");
        File.WriteAllText(settings, "not json");

        Assert.That(new PickerLocations(settings).Get(PickerKind.Catalog), Is.Null);
    }

    [Test]
    public void Remember_WritesUtf8WithoutByteOrderMark()
    {
        var settings = Path.Combine(_directory, "locations.json");
        var folder = Directory.CreateDirectory(Path.Combine(_directory, "catalogs")).FullName;

        new PickerLocations(settings).Remember(PickerKind.Catalog, folder);

        var bytes = File.ReadAllBytes(settings);
        Assert.Multiple(() =>
        {
            Assert.That(bytes[0], Is.Not.EqualTo(0xEF));
            Assert.That(bytes[^1], Is.EqualTo((byte)'\n'));
        });
    }
}
