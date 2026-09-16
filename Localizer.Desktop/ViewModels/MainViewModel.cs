using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Localizer.Application.Project;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
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
    private readonly InvalidateTranslationUseCase _invalidateTranslation;
    private readonly RequestTranslationDraftsUseCase _requestTranslationDrafts;
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
    private DispatcherTimer? _busyTimer;
    private int _busyTick;
    private string _busyBaseStatus = string.Empty;

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
        InvalidateTranslationUseCase invalidateTranslation,
        RequestTranslationDraftsUseCase requestTranslationDrafts,
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
        _invalidateTranslation = invalidateTranslation;
        _requestTranslationDrafts = requestTranslationDrafts;
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

    public bool CanRunCatalogCommands => HasCatalog && !IsBusy;

    public bool CanRunProjectCommands => HasProjectFolder && !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunCatalogCommands));
        OnPropertyChanged(nameof(CanRunProjectCommands));
        UpdateBusyStatusAnimation(value);
    }

    partial void OnHasCatalogChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunCatalogCommands));
    }

    partial void OnHasProjectFolderChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunProjectCommands));
    }

    private void UpdateBusyStatusAnimation(bool isBusy)
    {
        if (isBusy)
        {
            _busyTick = 0;
            _busyTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            _busyTimer.Tick -= OnBusyTimerTick;
            _busyTimer.Tick += OnBusyTimerTick;
            _busyTimer.Start();
            RefreshBusyStatusText();
            return;
        }

        _busyTimer?.Stop();
        _busyBaseStatus = string.Empty;
    }

    private void OnBusyTimerTick(object? sender, EventArgs e) => RefreshBusyStatusText();

    private void RefreshBusyStatusText()
    {
        if (string.IsNullOrWhiteSpace(_busyBaseStatus))
        {
            return;
        }

        var dots = (_busyTick % 3) switch
        {
            0 => ".",
            1 => "..",
            _ => "..."
        };
        _busyTick++;
        StatusText = $"{_busyBaseStatus}{dots}";
    }

    private void BeginBusyStatus(string baseStatus)
    {
        _busyBaseStatus = baseStatus;
        _busyTick = 0;
        RefreshBusyStatusText();
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
        catch (OperationCanceledException ex)
        {
            var timedOut = !token.IsCancellationRequested
                && (ex is TaskCanceledException
                    || ex.InnerException is TimeoutException
                    || ex.Message.Contains("HttpClient.Timeout", StringComparison.Ordinal));

            StatusText = timedOut
                ? "Drafting timed out waiting for Ollama. Try again or Cancel I/O and check the model."
                : "Operation cancelled.";
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
