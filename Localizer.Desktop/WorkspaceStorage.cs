namespace Localizer.Desktop;

internal static class WorkspaceStorage
{
    public const string FolderName = "Storage";
    public const string WorkspaceVariable = "LOCALIZER_WORKSPACE";

    public static string EnsureDirectory()
    {
        var storagePath = Environment.GetEnvironmentVariable(WorkspaceVariable);
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            var root = FindRepositoryRoot() ?? Directory.GetCurrentDirectory();
            storagePath = Path.Combine(root, FolderName);
        }

        Directory.CreateDirectory(storagePath);
        return storagePath;
    }

    private static string? FindRepositoryRoot()
    {
        foreach (var start in CandidateStarts())
        {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Localizer.slnx")))
                {
                    return directory.FullName;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateStarts()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;
    }
}
