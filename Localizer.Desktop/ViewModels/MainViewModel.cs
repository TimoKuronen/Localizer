using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Localizer.Application.Project;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Desktop.Services;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IUiDialogs _dialogs;
    private readonly IProjectFolderSettingsStore _projectFolderSettings;
    private readonly CreateCatalogUseCase _createCatalog;
    private readonly OpenCatalogUseCase _openCatalog;
    private readonly SaveCatalogUseCase _saveCatalog;
    private readonly AddCatalogEntryUseCase _addEntry;
    private readonly UpdateCatalogEntryUseCase _updateEntry;
    private readonly RemoveCatalogEntryUseCase _removeEntry;
    private readonly SetTranslationDraftUseCase _setTranslationDraft;
    private readonly ApproveTranslationUseCase _approveTranslation;
    private readonly ExportCatalogUseCase _exportCatalog;
    private readonly ImportUnityCsvUseCase _importUnityCsv;
    private readonly ExportUnityCsvUseCase _exportUnityCsv;
    private readonly GetCatalogStatusSummaryUseCase _getStatusSummary;
    private readonly ValidateCatalogUseCase _validateCatalog;

    private Catalog? _catalog;
    private string? _catalogPath;
    private ProjectFolderBinding? _projectFolderBinding;
    private CancellationTokenSource? _ioCts;
    private bool _suppressSelectionLoad;

    public MainViewModel(
        IUiDialogs dialogs,
        IProjectFolderSettingsStore projectFolderSettings,
        CreateCatalogUseCase createCatalog,
        OpenCatalogUseCase openCatalog,
        SaveCatalogUseCase saveCatalog,
        AddCatalogEntryUseCase addEntry,
        UpdateCatalogEntryUseCase updateEntry,
        RemoveCatalogEntryUseCase removeEntry,
        SetTranslationDraftUseCase setTranslationDraft,
        ApproveTranslationUseCase approveTranslation,
        ExportCatalogUseCase exportCatalog,
        ImportUnityCsvUseCase importUnityCsv,
        ExportUnityCsvUseCase exportUnityCsv,
        GetCatalogStatusSummaryUseCase getStatusSummary,
        ValidateCatalogUseCase validateCatalog)
    {
        _dialogs = dialogs;
        _projectFolderSettings = projectFolderSettings;
        _createCatalog = createCatalog;
        _openCatalog = openCatalog;
        _saveCatalog = saveCatalog;
        _addEntry = addEntry;
        _updateEntry = updateEntry;
        _removeEntry = removeEntry;
        _setTranslationDraft = setTranslationDraft;
        _approveTranslation = approveTranslation;
        _exportCatalog = exportCatalog;
        _importUnityCsv = importUnityCsv;
        _exportUnityCsv = exportUnityCsv;
        _getStatusSummary = getStatusSummary;
        _validateCatalog = validateCatalog;
    }

    public ObservableCollection<EntryRowViewModel> Entries { get; } = [];

    public ObservableCollection<TranslationEditViewModel> Translations { get; } = [];

    public ObservableCollection<DiagnosticRowViewModel> Diagnostics { get; } = [];

    [ObservableProperty]
    public partial EntryRowViewModel? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = "Localizer";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "No catalog open.";

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasCatalog { get; set; }

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial string EditorKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditorSourceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditorDeveloperNotes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanEditEntry { get; set; }

    [ObservableProperty]
    public partial bool HasProjectFolder { get; set; }

    [ObservableProperty]
    public partial string ProjectFolderText { get; set; } = "Project folder not set.";

    partial void OnSelectedEntryChanged(EntryRowViewModel? value) =>
        _ = HandleSelectedEntryChangedAsync(value);

    private async Task HandleSelectedEntryChangedAsync(EntryRowViewModel? value)
    {
        if (_suppressSelectionLoad)
        {
            return;
        }

        if (CanEditEntry && HasPendingEditorChanges())
        {
            var previousKey = EditorKey;
            var applied = await TryApplyPendingEditorChangesAsync(refreshUi: false).ConfigureAwait(true);
            if (!applied)
            {
                _suppressSelectionLoad = true;
                SelectedEntry = Entries.FirstOrDefault(entry => entry.Key == previousKey);
                _suppressSelectionLoad = false;
                return;
            }

            IsDirty = true;
            StatusText = $"Saved pending edits for '{previousKey}'.";
        }

        LoadSelectedEntry(value);
    }

    [RelayCommand]
    private async Task NewCatalogAsync()
    {
        var request = await _dialogs.PromptCreateCatalogAsync().ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        var result = _createCatalog.Execute(request);
        if (!result.Succeeded || result.Value is null)
        {
            await _dialogs.ShowMessageAsync("Create catalog failed", FormatError(result)).ConfigureAwait(true);
            return;
        }

        SetCatalog(result.Value, path: null, dirty: true);
        await LoadProjectFolderBindingAsync(result.Value.CatalogId.Value).ConfigureAwait(true);
        StatusText = "Created new catalog.";
    }

    [RelayCommand]
    private async Task OpenCatalogAsync(CancellationToken cancellationToken)
    {
        var path = await _dialogs.PickOpenCatalogPathAsync(cancellationToken).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        await RunIoAsync(async token =>
        {
            var result = await _openCatalog.ExecuteAsync(path, token).ConfigureAwait(true);
            if (!result.Succeeded || result.Value is null)
            {
                await _dialogs.ShowMessageAsync("Open catalog failed", FormatError(result)).ConfigureAwait(true);
                return;
            }

            SetCatalog(result.Value, path, dirty: false);
            await LoadProjectFolderBindingAsync(result.Value.CatalogId.Value).ConfigureAwait(true);
            StatusText = $"Opened {path}";
        }, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SetProjectFolderAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null)
        {
            return;
        }

        var folder = await _dialogs.PickExportDirectoryAsync(cancellationToken).ConfigureAwait(true);
        if (folder is null)
        {
            return;
        }

        var binding = await _dialogs
            .PromptProjectFolderAsync(folder, _projectFolderBinding)
            .ConfigureAwait(true);
        if (binding is null)
        {
            return;
        }

        binding = binding with { CatalogId = _catalog.CatalogId.Value };

        await _projectFolderSettings.SaveAsync(binding, cancellationToken).ConfigureAwait(true);
        ApplyProjectFolderBinding(binding);
        StatusText = $"Project folder set to {folder}.";
    }

    [RelayCommand]
    private async Task ImportFromProjectAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null || _projectFolderBinding is null || !_projectFolderBinding.IsConfigured)
        {
            return;
        }

        var importPath = _projectFolderBinding.ImportFilePath;
        if (!File.Exists(importPath))
        {
            await _dialogs.ShowMessageAsync(
                "Import failed",
                $"{UseCaseErrorCodes.ProjectFileNotFound}: Expected import file was not found at '{importPath}'.").ConfigureAwait(true);
            return;
        }

        await RunIoAsync(async token =>
        {
            var outcome = await _importUnityCsv.ExecuteAsync(_catalog, importPath, token).ConfigureAwait(true);
            if (!outcome.Succeeded)
            {
                await _dialogs.ShowMessageAsync(
                    "Import failed",
                    FormatImportError(outcome)).ConfigureAwait(true);
                return;
            }

            IsDirty = true;
            RefreshFromCatalog(selectKey: SelectedEntry?.Key);
            RunValidation();
            StatusText =
                $"Imported Unity CSV from {importPath}. Added {outcome.MergeOutcome.AddedCount}, updated {outcome.MergeOutcome.UpdatedCount}, unchanged {outcome.MergeOutcome.UnchangedCount}.";
        }, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExportToProjectAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null || _projectFolderBinding is null || !_projectFolderBinding.IsConfigured)
        {
            return;
        }

        var exportPath = _projectFolderBinding.ExportFilePath;

        await RunIoAsync(async token =>
        {
            var outcome = await _exportUnityCsv.ExecuteAsync(_catalog, exportPath, token).ConfigureAwait(true);
            if (!outcome.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Export failed", FormatUnityExportError(outcome)).ConfigureAwait(true);
                return;
            }

            StatusText = $"Exported Unity CSV to {exportPath}.";
            await _dialogs.ShowMessageAsync("Export complete", exportPath).ConfigureAwait(true);
        }, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveCatalogAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null)
        {
            return;
        }

        var path = _catalogPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = await _dialogs.PickSaveCatalogPathAsync(
                suggestedFileName: $"{_catalog.CatalogId.Value}.json",
                cancellationToken).ConfigureAwait(true);
            if (path is null)
            {
                return;
            }
        }

        await SaveToPathAsync(path, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveCatalogAsAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null)
        {
            return;
        }

        var path = await _dialogs.PickSaveCatalogPathAsync(
            suggestedFileName: $"{_catalog.CatalogId.Value}.json",
            cancellationToken).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        await SaveToPathAsync(path, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private void CancelIo() => _ioCts?.Cancel();

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (_catalog is null)
        {
            return;
        }

        var request = await _dialogs.PromptAddEntryAsync().ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        var result = _addEntry.Execute(_catalog, request);
        if (!result.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Add entry failed", FormatError(result)).ConfigureAwait(true);
            return;
        }

        IsDirty = true;
        RefreshFromCatalog(selectKey: request.Key);
        StatusText = $"Added entry '{request.Key}'.";
    }

    [RelayCommand]
    private async Task RemoveEntryAsync()
    {
        if (_catalog is null || SelectedEntry is null)
        {
            return;
        }

        var key = SelectedEntry.Key;
        var result = _removeEntry.Execute(_catalog, key);
        if (!result.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Remove entry failed", FormatError(result)).ConfigureAwait(true);
            return;
        }

        IsDirty = true;
        RefreshFromCatalog(selectKey: null);
        StatusText = $"Removed entry '{key}'.";
    }

    [RelayCommand]
    private async Task ApplyEntryEditsAsync()
    {
        if (_catalog is null || !CanEditEntry || string.IsNullOrWhiteSpace(EditorKey))
        {
            return;
        }

        var key = EditorKey;
        var applied = await TryApplyPendingEditorChangesAsync(refreshUi: true).ConfigureAwait(true);
        if (!applied)
        {
            return;
        }

        StatusText = $"Applied changes to '{key}'.";
    }

    private bool HasPendingEditorChanges()
    {
        if (_catalog is null || string.IsNullOrWhiteSpace(EditorKey))
        {
            return false;
        }

        if (!_catalog.Entries.TryGetValue(EntryKey.Create(EditorKey), out var existing))
        {
            return false;
        }

        if (!string.Equals(EditorSourceText, existing.SourceText, StringComparison.Ordinal))
        {
            return true;
        }

        var notes = string.IsNullOrWhiteSpace(EditorDeveloperNotes) ? null : EditorDeveloperNotes;
        if (!string.Equals(notes, existing.DeveloperNotes, StringComparison.Ordinal))
        {
            return true;
        }

        return Translations.Any(translation => translation.HasTextChanged);
    }

    private async Task<bool> TryApplyPendingEditorChangesAsync(bool refreshUi)
    {
        if (_catalog is null || !CanEditEntry || string.IsNullOrWhiteSpace(EditorKey))
        {
            return false;
        }

        var key = EditorKey;
        if (!_catalog.Entries.TryGetValue(EntryKey.Create(key), out var existing))
        {
            await _dialogs.ShowMessageAsync("Apply failed", $"No entry with key '{key}' exists.").ConfigureAwait(true);
            return false;
        }

        var updateResult = _updateEntry.Execute(_catalog, new UpdateCatalogEntryRequest
        {
            Key = key,
            SourceText = EditorSourceText,
            Category = existing.Category,
            AllowEmptyText = existing.AllowEmptyText,
            DeveloperNotes = string.IsNullOrWhiteSpace(EditorDeveloperNotes)
                ? null
                : EditorDeveloperNotes,
            Context = existing.Context,
            Constraints = existing.Constraints,
            SyntaxProfileOverride = existing.SyntaxProfileOverride,
            ExternalIds = existing.ExternalIds
        });

        if (!updateResult.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Apply failed", FormatError(updateResult)).ConfigureAwait(true);
            return false;
        }

        foreach (var translation in Translations)
        {
            if (!translation.HasTextChanged)
            {
                continue;
            }

            if (string.IsNullOrEmpty(translation.Text) && !existing.AllowEmptyText)
            {
                continue;
            }

            var draftResult = _setTranslationDraft.Execute(_catalog, new SetTranslationDraftRequest
            {
                Key = key,
                Locale = translation.Locale,
                Text = translation.Text
            });

            if (!draftResult.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Apply translation failed", FormatError(draftResult)).ConfigureAwait(true);
                return false;
            }
        }

        IsDirty = true;
        if (refreshUi)
        {
            RefreshFromCatalog(selectKey: key);
            RunValidation();
        }

        return true;
    }

    [RelayCommand]
    private void ValidateCatalog()
    {
        if (_catalog is null)
        {
            return;
        }

        RunValidation();
        StatusText = Diagnostics.Count == 0
            ? "Validation found no diagnostics."
            : $"Validation reported {Diagnostics.Count} diagnostic(s).";
    }

    [RelayCommand]
    private async Task ExportCatalogAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null)
        {
            return;
        }

        var directory = await _dialogs.PickExportDirectoryAsync(cancellationToken).ConfigureAwait(true);
        if (directory is null)
        {
            return;
        }

        await RunIoAsync(async token =>
        {
            var outcome = await _exportCatalog.ExecuteAsync(_catalog, directory, token).ConfigureAwait(true);
            if (!outcome.Succeeded)
            {
                Diagnostics.Clear();
                foreach (var diagnostic in outcome.Validation.Diagnostics)
                {
                    Diagnostics.Add(new DiagnosticRowViewModel(
                        diagnostic.Severity.ToString(),
                        diagnostic.Code,
                        diagnostic.Message,
                        diagnostic.EntryKey?.Value,
                        diagnostic.Locale?.Value));
                }

                var message = string.IsNullOrWhiteSpace(outcome.ErrorCode)
                    ? outcome.ErrorMessage ?? "Export failed."
                    : $"{outcome.ErrorCode}: {outcome.ErrorMessage}";
                if (outcome.Validation.Diagnostics.Count > 0)
                {
                    message += $"{Environment.NewLine}{Environment.NewLine}See diagnostics panel for {outcome.Validation.Diagnostics.Count} finding(s).";
                }

                await _dialogs.ShowMessageAsync("Export failed", message).ConfigureAwait(true);
                StatusText = "Export blocked.";
                return;
            }

            StatusText = $"Exported {outcome.WrittenFiles.Count} locale file(s) to {directory}.";
            await _dialogs.ShowMessageAsync(
                "Export complete",
                string.Join(Environment.NewLine, outcome.WrittenFiles)).ConfigureAwait(true);
        }, cancellationToken).ConfigureAwait(true);
    }

    private async Task ApproveTranslationRowAsync(TranslationEditViewModel row)
    {
        if (_catalog is null || string.IsNullOrWhiteSpace(EditorKey))
        {
            return;
        }

        var key = EditorKey;

        if (row.HasTextChanged)
        {
            if (string.IsNullOrEmpty(row.Text))
            {
                await _dialogs.ShowMessageAsync(
                    "Approve failed",
                    "Translation text is empty. Enter text before approving.").ConfigureAwait(true);
                return;
            }

            var draftResult = _setTranslationDraft.Execute(_catalog, new SetTranslationDraftRequest
            {
                Key = key,
                Locale = row.Locale,
                Text = row.Text
            });

            if (!draftResult.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Approve failed", FormatError(draftResult)).ConfigureAwait(true);
                return;
            }
        }

        var approveResult = _approveTranslation.Execute(_catalog, new ApproveTranslationRequest
        {
            Key = key,
            Locale = row.Locale
        });

        if (!approveResult.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Approve failed", FormatError(approveResult)).ConfigureAwait(true);
            RunValidation();
            return;
        }

        IsDirty = true;
        RefreshFromCatalog(selectKey: key);
        RunValidation();
        StatusText = $"Approved '{key}' for {row.Locale}.";
    }

    private async Task SaveToPathAsync(string path, CancellationToken cancellationToken)
    {
        if (_catalog is null)
        {
            return;
        }

        await RunIoAsync(async token =>
        {
            var result = await _saveCatalog.ExecuteAsync(path, _catalog, token).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Save catalog failed", FormatError(result)).ConfigureAwait(true);
                return;
            }

            _catalogPath = path;
            IsDirty = false;
            UpdateWindowTitle();
            StatusText = $"Saved {path}";
        }, cancellationToken).ConfigureAwait(true);
    }

    private async Task RunIoAsync(Func<CancellationToken, Task> action, CancellationToken externalToken)
    {
        _ioCts?.Cancel();
        _ioCts?.Dispose();
        _ioCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var token = _ioCts.Token;

        IsBusy = true;
        try
        {
            await action(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Operation cancelled.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetCatalog(Catalog catalog, string? path, bool dirty)
    {
        _catalog = catalog;
        _catalogPath = path;
        HasCatalog = true;
        IsDirty = dirty;
        RefreshFromCatalog(selectKey: catalog.Entries.Keys.Select(key => key.Value).OrderBy(key => key, StringComparer.Ordinal).FirstOrDefault());
        RunValidation();
        UpdateWindowTitle();
    }

    private async Task LoadProjectFolderBindingAsync(string catalogId)
    {
        var binding = await _projectFolderSettings.GetAsync(catalogId).ConfigureAwait(true);
        ApplyProjectFolderBinding(binding);
    }

    private void ApplyProjectFolderBinding(ProjectFolderBinding? binding)
    {
        _projectFolderBinding = binding;
        HasProjectFolder = binding?.IsConfigured == true;
        ProjectFolderText = binding?.IsConfigured == true
            ? $"{binding.FolderPath} | import: {binding.ImportFileName} | export: {binding.ExportFileName}"
            : "Project folder not set.";
    }

    private void RefreshFromCatalog(string? selectKey)
    {
        if (_catalog is null)
        {
            Entries.Clear();
            Translations.Clear();
            Diagnostics.Clear();
            CanEditEntry = false;
            SummaryText = string.Empty;
            return;
        }

        var summary = _getStatusSummary.Execute(_catalog);
        SummaryText =
            $"Entries {summary.EntryCount} | Missing {summary.MissingCount} | Stale {summary.StaleCount} | Draft {summary.DraftCount} | Approved {summary.ApprovedCount}";

        var previousKey = selectKey ?? SelectedEntry?.Key;
        _suppressSelectionLoad = true;
        Entries.Clear();

        foreach (var entry in _catalog.Entries.Values.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal))
        {
            var statuses = _catalog.RequiredLocales
                .OrderBy(locale => locale.Value, StringComparer.Ordinal)
                .Select(locale => $"{locale.Value}:{_catalog.GetEffectiveStatus(entry, locale)}")
                .ToArray();

            Entries.Add(new EntryRowViewModel(
                entry.Key.Value,
                entry.SourceText,
                string.Join(" | ", statuses)));
        }

        _suppressSelectionLoad = false;
        SelectedEntry = Entries.FirstOrDefault(entry => entry.Key == previousKey) ?? Entries.FirstOrDefault();
        if (SelectedEntry is null)
        {
            ClearEditor();
        }
    }

    private void LoadSelectedEntry(EntryRowViewModel? row)
    {
        if (_suppressSelectionLoad)
        {
            return;
        }

        if (_catalog is null || row is null)
        {
            ClearEditor();
            return;
        }

        if (!_catalog.Entries.TryGetValue(EntryKey.Create(row.Key), out var entry))
        {
            ClearEditor();
            return;
        }

        EditorKey = entry.Key.Value;
        EditorSourceText = entry.SourceText;
        EditorDeveloperNotes = entry.DeveloperNotes ?? string.Empty;
        CanEditEntry = true;

        Translations.Clear();
        foreach (var locale in _catalog.RequiredLocales.OrderBy(locale => locale.Value, StringComparer.Ordinal))
        {
            entry.Translations.TryGetValue(locale, out var translation);
            var status = _catalog.GetEffectiveStatus(entry, locale);
            Translations.Add(new TranslationEditViewModel(
                locale.Value,
                translation?.Text ?? string.Empty,
                status,
                ApproveTranslationRowAsync));
        }
    }

    private void ClearEditor()
    {
        EditorKey = string.Empty;
        EditorSourceText = string.Empty;
        EditorDeveloperNotes = string.Empty;
        CanEditEntry = false;
        Translations.Clear();
    }

    private void RunValidation()
    {
        Diagnostics.Clear();
        if (_catalog is null)
        {
            return;
        }

        var result = _validateCatalog.Execute(_catalog);
        foreach (var diagnostic in result.Diagnostics)
        {
            Diagnostics.Add(new DiagnosticRowViewModel(
                diagnostic.Severity.ToString(),
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.EntryKey?.Value,
                diagnostic.Locale?.Value));
        }
    }

    private void UpdateWindowTitle()
    {
        if (_catalog is null)
        {
            WindowTitle = "Localizer";
            return;
        }

        var name = _catalogPath ?? $"{_catalog.CatalogId.Value} (unsaved)";
        WindowTitle = IsDirty ? $"Localizer - {name}*" : $"Localizer - {name}";
    }

    private static string FormatError(UseCaseResult result) =>
        string.IsNullOrWhiteSpace(result.ErrorCode)
            ? result.ErrorMessage ?? "Unknown error."
            : $"{result.ErrorCode}: {result.ErrorMessage}";

    private static string FormatError<T>(UseCaseResult<T> result) =>
        string.IsNullOrWhiteSpace(result.ErrorCode)
            ? result.ErrorMessage ?? "Unknown error."
            : $"{result.ErrorCode}: {result.ErrorMessage}";

    private static string FormatImportError(ImportUnityCsvOutcome outcome) =>
        string.IsNullOrWhiteSpace(outcome.ErrorCode)
            ? outcome.ErrorMessage ?? "Import failed."
            : $"{outcome.ErrorCode}: {outcome.ErrorMessage}";

    private static string FormatUnityExportError(ExportUnityCsvOutcome outcome) =>
        string.IsNullOrWhiteSpace(outcome.ErrorCode)
            ? outcome.ErrorMessage ?? "Export failed."
            : $"{outcome.ErrorCode}: {outcome.ErrorMessage}";
}
