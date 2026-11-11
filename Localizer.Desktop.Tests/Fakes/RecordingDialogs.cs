using Avalonia.Controls;
using Localizer.Application.Project;
using Localizer.Application.UseCases;
using Localizer.Desktop.Services;

namespace Localizer.Desktop.Tests.Fakes;

internal sealed class RecordingDialogs : IUiDialogs
{
    public string? OpenPath { get; set; }

    public string? SavePath { get; set; }

    public ProjectFolderBinding? ProjectFolderResult { get; set; }

    public CreateCatalogRequest? CreateResult { get; set; }

    public int SavePickCount { get; private set; }

    public int OpenPickCount { get; private set; }

    public int CreatePromptCount { get; private set; }

    public List<string> Messages { get; } = [];

    public void Attach(Window owner)
    {
    }

    public Task<string?> PickOpenCatalogPathAsync(CancellationToken cancellationToken = default)
    {
        OpenPickCount++;
        return Task.FromResult(OpenPath);
    }

    public Task<string?> PickSaveCatalogPathAsync(
        string? suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        SavePickCount++;
        return Task.FromResult(SavePath);
    }

    public Task<string?> PickExportDirectoryAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<ProjectFolderBinding?> PromptProjectFolderAsync(
        string folderPath,
        ProjectFolderBinding? existing = null) =>
        Task.FromResult(ProjectFolderResult);

    public Task<CreateCatalogRequest?> PromptCreateCatalogAsync()
    {
        CreatePromptCount++;
        return Task.FromResult(CreateResult);
    }

    public Task<AddCatalogEntryRequest?> PromptAddEntryAsync() =>
        Task.FromResult<AddCatalogEntryRequest?>(null);

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add($"{title}: {message}");
        return Task.CompletedTask;
    }
}
