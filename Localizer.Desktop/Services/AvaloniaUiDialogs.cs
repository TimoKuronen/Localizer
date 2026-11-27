using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Localizer.Application.Project;
using Localizer.Application.UseCases;
using Localizer.Core.Syntax;
using Localizer.Desktop.ViewModels;
using Localizer.Desktop.Views;

namespace Localizer.Desktop.Services;

public sealed class AvaloniaUiDialogs : IUiDialogs
{
    private readonly PickerLocations _locations;
    private Window? _owner;

    public AvaloniaUiDialogs(PickerLocations locations) =>
        _locations = locations ?? throw new ArgumentNullException(nameof(locations));

    public void Attach(Window owner) =>
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public async Task<string?> PickOpenCatalogPathAsync(CancellationToken cancellationToken = default)
    {
        var owner = RequireOwner();
        cancellationToken.ThrowIfCancellationRequested();

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open catalog",
            AllowMultiple = false,
            FileTypeFilter = [CatalogFileType],
            SuggestedStartLocation = await GetStartFolderAsync(owner, PickerKind.Catalog).ConfigureAwait(true)
        }).ConfigureAwait(true);

        return RememberFile(PickerKind.Catalog, files.Count == 0 ? null : files[0].TryGetLocalPath());
    }

    public async Task<string?> PickSourceStringsPathAsync(CancellationToken cancellationToken = default)
    {
        var owner = RequireOwner();
        cancellationToken.ThrowIfCancellationRequested();

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import source strings",
            AllowMultiple = false,
            FileTypeFilter = [SourceStringsFileType],
            SuggestedStartLocation = await GetStartFolderAsync(owner, PickerKind.SourceStrings).ConfigureAwait(true)
        }).ConfigureAwait(true);

        return RememberFile(PickerKind.SourceStrings, files.Count == 0 ? null : files[0].TryGetLocalPath());
    }

    public async Task<string?> PickComparisonSourcePathAsync(CancellationToken cancellationToken = default)
    {
        var owner = RequireOwner();
        cancellationToken.ThrowIfCancellationRequested();

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Compare with source file",
            AllowMultiple = false,
            FileTypeFilter = [ComparisonSourceFileType],
            SuggestedStartLocation = await GetStartFolderAsync(owner, PickerKind.SourceStrings).ConfigureAwait(true)
        }).ConfigureAwait(true);

        return RememberFile(PickerKind.SourceStrings, files.Count == 0 ? null : files[0].TryGetLocalPath());
    }

    public async Task<string?> PickSaveCatalogPathAsync(
        string? suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        var owner = RequireOwner();
        cancellationToken.ThrowIfCancellationRequested();

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save catalog",
            SuggestedFileName = suggestedFileName ?? "catalog.json",
            DefaultExtension = "json",
            FileTypeChoices = [CatalogFileType],
            SuggestedStartLocation = await GetStartFolderAsync(owner, PickerKind.Catalog).ConfigureAwait(true)
        }).ConfigureAwait(true);

        return RememberFile(PickerKind.Catalog, file?.TryGetLocalPath());
    }

    public async Task<string?> PickExportDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var owner = RequireOwner();
        cancellationToken.ThrowIfCancellationRequested();

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Export locale files",
            AllowMultiple = false,
            SuggestedStartLocation = await GetStartFolderAsync(owner, PickerKind.ExportDirectory).ConfigureAwait(true)
        }).ConfigureAwait(true);

        var folder = folders.Count == 0 ? null : folders[0].TryGetLocalPath();
        _locations.Remember(PickerKind.ExportDirectory, folder);
        return folder;
    }

    public async Task<string?> PickPreviewDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var owner = RequireOwner();
        cancellationToken.ThrowIfCancellationRequested();

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Draft preview folder",
            AllowMultiple = false,
            SuggestedStartLocation = await GetStartFolderAsync(owner, PickerKind.PreviewDirectory).ConfigureAwait(true)
        }).ConfigureAwait(true);

        var folder = folders.Count == 0 ? null : folders[0].TryGetLocalPath();
        _locations.Remember(PickerKind.PreviewDirectory, folder);
        return folder;
    }

    public async Task<ProjectFolderBinding?> PromptProjectFolderAsync(
        string folderPath,
        ProjectFolderBinding? existing = null)
    {
        var owner = RequireOwner();
        var viewModel = new ProjectFolderViewModel(
            folderPath,
            ListCsvFileNames(folderPath),
            existing?.ImportFileName,
            existing?.ExportFileName);
        var window = new ProjectFolderWindow
        {
            DataContext = viewModel
        };

        var accepted = await window.ShowDialog<bool>(owner).ConfigureAwait(true);
        if (!accepted)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(viewModel.ImportFileName)
            || string.IsNullOrWhiteSpace(viewModel.ExportFileName))
        {
            return null;
        }

        return new ProjectFolderBinding
        {
            CatalogId = existing?.CatalogId ?? string.Empty,
            FolderPath = folderPath,
            ImportFileName = viewModel.ImportFileName.Trim(),
            ExportFileName = viewModel.ExportFileName.Trim()
        };
    }

    // Top-level only: the bound folder is the one holding the exchanged CSV, not a project root to crawl.
    private static IReadOnlyList<string> ListCsvFileNames(string folderPath)
    {
        try
        {
            return Directory
                .EnumerateFiles(folderPath, "*.csv", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Order(StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public async Task<CreateCatalogRequest?> PromptCreateCatalogAsync()
    {
        var owner = RequireOwner();
        var viewModel = new NewCatalogViewModel();
        var window = new NewCatalogWindow
        {
            DataContext = viewModel
        };

        var accepted = await window.ShowDialog<bool>(owner).ConfigureAwait(true);
        if (!accepted)
        {
            return null;
        }

        var required = viewModel.RequiredLocalesText
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return new CreateCatalogRequest
        {
            CatalogId = viewModel.CatalogId.Trim(),
            SourceLocale = viewModel.SourceLocale.Trim(),
            RequiredLocales = required,
            DefaultSyntaxProfile = viewModel.UseCompositeSyntax
                ? MessageSyntaxProfile.Composite
                : MessageSyntaxProfile.Plain
        };
    }

    public async Task<AddCatalogEntryRequest?> PromptAddEntryAsync()
    {
        var owner = RequireOwner();
        var viewModel = new AddEntryViewModel();
        var window = new AddEntryWindow
        {
            DataContext = viewModel
        };

        var accepted = await window.ShowDialog<bool>(owner).ConfigureAwait(true);
        if (!accepted)
        {
            return null;
        }

        return new AddCatalogEntryRequest
        {
            Key = viewModel.Key.Trim(),
            SourceText = viewModel.SourceText,
            DeveloperNotes = string.IsNullOrWhiteSpace(viewModel.DeveloperNotes)
                ? null
                : viewModel.DeveloperNotes.Trim()
        };
    }

    // Closing the window with the title-bar button counts as Cancel.
    public async Task<UnsavedChangesChoice> PromptUnsavedChangesAsync(string message)
    {
        var owner = RequireOwner();
        var window = new UnsavedChangesWindow
        {
            DataContext = new MessageViewModel { Message = message }
        };

        return await window.ShowDialog<UnsavedChangesChoice>(owner).ConfigureAwait(true);
    }

    public async Task<ConfigureCatalogSettingsRequest?> PromptCatalogSettingsAsync(
        ConfigureCatalogSettingsRequest current)
    {
        var owner = RequireOwner();
        var viewModel = new CatalogSettingsViewModel(current);
        var window = new CatalogSettingsWindow
        {
            DataContext = viewModel
        };

        var accepted = await window.ShowDialog<bool>(owner).ConfigureAwait(true);
        return accepted ? viewModel.ToRequest() : null;
    }

    public async Task<DraftModelChoice?> PromptDraftModelAsync(IReadOnlyList<string> models, string? savedModel)
    {
        var owner = RequireOwner();
        var viewModel = new DraftModelViewModel(models, savedModel);
        var window = new DraftModelWindow
        {
            DataContext = viewModel
        };

        var accepted = await window.ShowDialog<bool>(owner).ConfigureAwait(true);
        return accepted ? new DraftModelChoice(viewModel.SelectedModelName) : null;
    }

    public async Task<IReadOnlyList<string>?> PromptChecklistAsync(ChecklistViewModel checklist)
    {
        var owner = RequireOwner();
        var window = new ChecklistWindow
        {
            DataContext = checklist
        };

        var accepted = await window.ShowDialog<bool>(owner).ConfigureAwait(true);
        return accepted ? checklist.SelectedIds : null;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var owner = RequireOwner();
        var window = new MessageWindow
        {
            Title = title,
            DataContext = new MessageViewModel { Message = message }
        };

        await window.ShowDialog(owner).ConfigureAwait(true);
    }

    public async Task ShowReportAsync(string title, string report)
    {
        var owner = RequireOwner();
        var window = new ReportWindow
        {
            Title = title,
            DataContext = new MessageViewModel { Message = report }
        };

        await window.ShowDialog(owner).ConfigureAwait(true);
    }

    public async Task ShowShortcutsAsync(IReadOnlyList<ShortcutInfo> shortcuts)
    {
        var owner = RequireOwner();
        var window = new ShortcutsWindow
        {
            DataContext = new ShortcutsViewModel(shortcuts)
        };

        await window.ShowDialog(owner).ConfigureAwait(true);
    }

    public async Task ShowWorkflowGuideAsync(IReadOnlyList<WorkflowSection> sections)
    {
        var owner = RequireOwner();
        var window = new WorkflowGuideWindow
        {
            DataContext = new WorkflowGuideViewModel(sections)
        };

        await window.ShowDialog(owner).ConfigureAwait(true);
    }

    private Window RequireOwner() =>
        _owner ?? throw new InvalidOperationException("UI dialogs are not attached to a window.");

    private string? RememberFile(PickerKind kind, string? path)
    {
        if (path is not null)
        {
            _locations.Remember(kind, Path.GetDirectoryName(path));
        }

        return path;
    }

    private async Task<IStorageFolder?> GetStartFolderAsync(Window owner, PickerKind kind)
    {
        var path = _locations.Get(kind) ?? WorkspaceStorage.EnsureDirectory();
        return await owner.StorageProvider.TryGetFolderFromPathAsync(path).ConfigureAwait(true);
    }

    private static FilePickerFileType CatalogFileType { get; } = new("Localizer catalog")
    {
        Patterns = ["*.json"]
    };

    private static FilePickerFileType ComparisonSourceFileType { get; } = new("Source strings JSON or CSV")
    {
        Patterns = ["*.json", "*.csv"]
    };

    private static FilePickerFileType SourceStringsFileType { get; } = new("Source strings")
    {
        Patterns = ["*.json"]
    };
}
