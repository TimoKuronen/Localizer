namespace Localizer.Desktop.Tests;

public sealed class WorkspaceStorageTests
{
    private string? _previousWorkspace;

    [SetUp]
    public void RememberWorkspace() =>
        _previousWorkspace = Environment.GetEnvironmentVariable(WorkspaceStorage.WorkspaceVariable);

    [TearDown]
    public void RestoreWorkspace() =>
        Environment.SetEnvironmentVariable(WorkspaceStorage.WorkspaceVariable, _previousWorkspace);

    [Test]
    public void EnsureDirectory_CreatesAndUsesTheConfiguredWorkspace()
    {
        var parent = Directory.CreateTempSubdirectory("localizer-workspace-tests-").FullName;
        var workspace = Path.Combine(parent, "workspace");
        Environment.SetEnvironmentVariable(WorkspaceStorage.WorkspaceVariable, workspace);

        try
        {
            Assert.That(WorkspaceStorage.EnsureDirectory(), Is.EqualTo(workspace));
            Assert.That(Directory.Exists(workspace), Is.True);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Test]
    public void EnsureDirectory_FallsBackToStorageWhenNoWorkspaceIsConfigured()
    {
        Environment.SetEnvironmentVariable(WorkspaceStorage.WorkspaceVariable, null);

        var path = WorkspaceStorage.EnsureDirectory();

        Assert.That(Path.GetFileName(path), Is.EqualTo(WorkspaceStorage.FolderName));
    }
}
