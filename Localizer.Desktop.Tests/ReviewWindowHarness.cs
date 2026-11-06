using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Localizer.Application.Drafting;
using Localizer.Application.Project;
using Localizer.Application.Time;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Desktop.Services;
using Localizer.Desktop.ViewModels;
using Localizer.Desktop.Views;
using Localizer.Infrastructure.Export;
using Localizer.Infrastructure.Import;
using Localizer.Infrastructure.Persistence.Json;

namespace Localizer.Desktop.Tests;

/// <summary>
/// Hosts the real main window headlessly on a catalog file, so tests exercise key routing and focus.
/// </summary>
internal sealed class ReviewWindowHarness : IDisposable
{
    private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(10);

    private readonly string _directory;
    private readonly JsonCatalogStore _store = new();
    private readonly RecordingDialogs _dialogs = new();

    private ReviewWindowHarness(Catalog catalog)
    {
        _directory = Directory.CreateTempSubdirectory("localizer-desktop-tests-").FullName;
        CatalogPath = Path.Combine(_directory, "catalog.json");

        var save = new SaveCatalogUseCase(_store);
        var saved = Task.Run(() => save.ExecuteAsync(CatalogPath, catalog)).GetAwaiter().GetResult();
        Assert.That(saved.Succeeded, Is.True, saved.ErrorMessage);

        _dialogs.OpenPath = CatalogPath;
        ViewModel = CreateViewModel();
        Window = new MainWindow { DataContext = ViewModel };
        Window.Show();

        ViewModel.OpenCatalogCommand.Execute(null);
        PumpUntil(() => ViewModel.Entries.Count > 0);
    }

    public string CatalogPath { get; }

    public MainViewModel ViewModel { get; }

    public MainWindow Window { get; }

    public IReadOnlyList<string> Messages => _dialogs.Messages;

    public string? SelectedKey => ViewModel.SelectedEntry?.Key;

    public string? ReviewTarget =>
        ViewModel.Translations.FirstOrDefault(translation => translation.IsReviewTarget)?.Locale;

    public object? FocusedElement => Window.FocusManager?.GetFocusedElement();

    public DataGrid EntriesGrid => Window.FindControl<DataGrid>("EntriesGrid")!;

    public static ReviewWindowHarness Open(Catalog catalog) => new(catalog);

    public void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    public void Type(string text)
    {
        Window.KeyTextInput(text);
        Pump();
    }

    public void ClickTranslationText(string locale) => Click(TranslationTextBox(locale));

    public void ClickApprove(string locale) =>
        Click(Window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content as string == "Approve"
                && button.DataContext is TranslationEditViewModel row
                && row.Locale == locale));

    public TextBox TranslationTextBox(string locale) =>
        Window.GetVisualDescendants()
            .OfType<TextBox>()
            .Single(box => box.DataContext is TranslationEditViewModel row && row.Locale == locale);

    public TranslationEditViewModel Translation(string locale) =>
        ViewModel.Translations.Single(translation => translation.Locale == locale);

    public Catalog ReadSavedCatalog()
    {
        var open = new OpenCatalogUseCase(_store);
        var result = Task.Run(() => open.ExecuteAsync(CatalogPath)).GetAwaiter().GetResult();
        Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
        return result.Value!;
    }

    public void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + PumpTimeout;
        while (!condition())
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "Timed out waiting for the window.");
            Pump();
            Thread.Sleep(1);
        }

        Pump();
    }

    public void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose()
    {
        Window.Close();
        Directory.Delete(_directory, recursive: true);
    }

    private void Click(Control control)
    {
        Pump();
        control.BringIntoView();
        Pump();
        var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var point = control.TranslatePoint(center, Window)
            ?? throw new InvalidOperationException("Control is not in the window.");

        Window.MouseMove(point);
        Window.MouseDown(point, MouseButton.Left);
        Window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    private MainViewModel CreateViewModel()
    {
        var getWorkQueue = new GetWorkQueueUseCase();
        var setTranslationDraft = new SetTranslationDraftUseCase();

        return new MainViewModel(
            _dialogs,
            new NoProjectFolderSettings(),
            new CreateCatalogUseCase(),
            new OpenCatalogUseCase(_store),
            new SaveCatalogUseCase(_store),
            new AddCatalogEntryUseCase(),
            new UpdateCatalogEntryUseCase(),
            new RemoveCatalogEntryUseCase(),
            setTranslationDraft,
            new ApproveTranslationUseCase(),
            new InvalidateTranslationUseCase(),
            new RequestTranslationDraftsUseCase(
                new EmptyDraftProvider(),
                new FixedClock(),
                getWorkQueue,
                setTranslationDraft),
            new ExportCatalogUseCase(new CompactLocaleJsonExporter()),
            new ImportUnityCsvUseCase(new UnityCsvReader(), new MergeUnityCsvImportUseCase()),
            new ExportUnityCsvUseCase(new UnityCsvExporter()),
            new GetCatalogStatusSummaryUseCase(),
            new ValidateCatalogUseCase(),
            new FindAdjacentDraftUseCase(getWorkQueue));
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class EmptyDraftProvider : ITranslationDraftProvider
    {
        public Task<TranslationDraftBatchResult> DraftAsync(
            TranslationDraftBatchRequest request,
            IProgress<TranslationDraftProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TranslationDraftBatchResult
            {
                ProviderName = "test",
                ModelName = "test-model",
                Items = []
            });
    }

    private sealed class NoProjectFolderSettings : IProjectFolderSettingsStore
    {
        public Task<ProjectFolderBinding?> GetAsync(
            string catalogId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectFolderBinding?>(null);

        public Task SaveAsync(ProjectFolderBinding binding, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingDialogs : IUiDialogs
    {
        public string? OpenPath { get; set; }

        public List<string> Messages { get; } = [];

        public void Attach(Window owner)
        {
        }

        public Task<string?> PickOpenCatalogPathAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(OpenPath);

        public Task<string?> PickSaveCatalogPathAsync(
            string? suggestedFileName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickExportDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<ProjectFolderBinding?> PromptProjectFolderAsync(
            string folderPath,
            ProjectFolderBinding? existing = null) =>
            Task.FromResult<ProjectFolderBinding?>(null);

        public Task<CreateCatalogRequest?> PromptCreateCatalogAsync() =>
            Task.FromResult<CreateCatalogRequest?>(null);

        public Task<AddCatalogEntryRequest?> PromptAddEntryAsync() =>
            Task.FromResult<AddCatalogEntryRequest?>(null);

        public Task ShowMessageAsync(string title, string message)
        {
            Messages.Add($"{title}: {message}");
            return Task.CompletedTask;
        }
    }
}
