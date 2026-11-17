namespace Localizer.Infrastructure.Tests;

internal static class TempDirectory
{
    /// <summary>
    /// Creates a new empty directory under the system temp folder, grouped by area name.
    /// </summary>
    public static string Create(string area)
    {
        var directory = Path.Combine(Path.GetTempPath(), area, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
