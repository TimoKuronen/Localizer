using Avalonia.Controls;
using Localizer.Application.Project;
using Localizer.Application.UseCases;
using Localizer.Desktop.Services;
using Localizer.Desktop.ViewModels;

namespace Localizer.Desktop.Tests.Fakes;

internal sealed class RecordingDialogs : IUiDialogs
{
    public string? OpenPath { get; set; }

    public string? SavePath { get; set; }

    public ProjectFolderBinding? ProjectFolderResult { get; set; }

    public CreateCatalogRequest? CreateResult { get; set; }

    public UnsavedChangesChoice UnsavedChoice { get; set; } = UnsavedChangesChoice.Cancel;

    public ConfigureCatalogSettingsRequest? SettingsResult { get; set; }

    public ConfigureCatalogSettingsRequest? PromptedSettings { get; private set; }

    public DraftModelChoice? ModelResult { get; set; }

    public IReadOnlyList<string>? PromptedModels { get; private set; }

    public string? PromptedSavedModel { get; private set; }

    public int UnsavedPromptCount { get; private set; }

    public int ModelPromptCount { get; private set; }

    public List<string> UnsavedPrompts { get; } = [];

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

    public string? SourceStringsPath { get; set; }

    public Task<string?> PickSourceStringsPathAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceStringsPath);

    public string? ComparisonSourcePath { get; set; }

    public int ComparisonPickCount { get; private set; }

    /// <summary>
    /// Answers a checklist; unset means the user cancels.
    /// </summary>
    public Func<ChecklistViewModel, IReadOnlyList<string>?>? ChecklistAnswer { get; set; }

    public List<ChecklistViewModel> Checklists { get; } = [];

    public Task<string?> PickComparisonSourcePathAsync(CancellationToken cancellationToken = default)
    {
        ComparisonPickCount++;
        return Task.FromResult(ComparisonSourcePath);
    }

    public Task<IReadOnlyList<string>?> PromptChecklistAsync(ChecklistViewModel checklist)
    {
        Checklists.Add(checklist);
        return Task.FromResult(ChecklistAnswer?.Invoke(checklist));
    }

    public Task<string?> PickSaveCatalogPathAsync(
        string? suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        SavePickCount++;
        return Task.FromResult(SavePath);
    }

    public string? PreviewDirectory { get; set; }

    public List<string> Reports { get; } = [];

    public int ShortcutsShownCount { get; private set; }

    public IReadOnlyList<ShortcutInfo>? ShownShortcuts { get; private set; }

    public Task<string?> PickPreviewDirectoryAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(PreviewDirectory);

    public Task ShowReportAsync(string title, string report)
    {
        Reports.Add($"{title}: {report}");
        return Task.CompletedTask;
    }

    public Task ShowShortcutsAsync(IReadOnlyList<ShortcutInfo> shortcuts)
    {
        ShortcutsShownCount++;
        ShownShortcuts = shortcuts;
        return Task.CompletedTask;
    }

    public int WorkflowGuideShownCount { get; private set; }

    public IReadOnlyList<WorkflowSection>? ShownWorkflowGuide { get; private set; }

    public Task ShowWorkflowGuideAsync(IReadOnlyList<WorkflowSection> sections)
    {
        WorkflowGuideShownCount++;
        ShownWorkflowGuide = sections;
        return Task.CompletedTask;
    }

    public string? ExportDirectory { get; set; }

    public Task<string?> PickExportDirectoryAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ExportDirectory);

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

    public Task<UnsavedChangesChoice> PromptUnsavedChangesAsync(string message)
    {
        UnsavedPromptCount++;
        UnsavedPrompts.Add(message);
        return Task.FromResult(UnsavedChoice);
    }

    public Task<ConfigureCatalogSettingsRequest?> PromptCatalogSettingsAsync(ConfigureCatalogSettingsRequest current)
    {
        PromptedSettings = current;
        return Task.FromResult(SettingsResult);
    }

    public Task<DraftModelChoice?> PromptDraftModelAsync(IReadOnlyList<string> models, string? savedModel)
    {
        ModelPromptCount++;
        PromptedModels = models;
        PromptedSavedModel = savedModel;
        return Task.FromResult(ModelResult);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add($"{title}: {message}");
        return Task.CompletedTask;
    }
}
