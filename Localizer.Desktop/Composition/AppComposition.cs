using Localizer.Application.Export;
using Localizer.Application.Import;
using Localizer.Application.Persistence;
using Localizer.Application.Project;
using Localizer.Application.UseCases;
using Localizer.Desktop.Services;
using Localizer.Desktop.ViewModels;
using Localizer.Infrastructure.Drafting;
using Localizer.Infrastructure.Export;
using Localizer.Infrastructure.Import;
using Localizer.Infrastructure.Persistence.Json;
using Localizer.Infrastructure.Time;

namespace Localizer.Desktop.Composition;

public static class AppComposition
{
    public static (MainViewModel MainViewModel, IUiDialogs Dialogs) Create()
    {
        ICatalogStore store = new JsonCatalogStore();
        ICatalogExporter exporter = new CompactLocaleJsonExporter();
        IUnityCsvExporter unityCsvExporter = new UnityCsvExporter();
        IUnityCsvReader unityCsvReader = new UnityCsvReader();
        var projectFolderSettingsPath = Path.Combine(WorkspaceStorage.EnsureDirectory(), "project-folders.json");
        IProjectFolderSettingsStore projectFolderSettings = new JsonProjectFolderSettingsStore(projectFolderSettingsPath);
        IUiDialogs dialogs = new AvaloniaUiDialogs();

        var ollamaOptions = CreateOllamaOptions();
        var httpClient = new HttpClient
        {
            BaseAddress = ollamaOptions.BaseAddress,
            Timeout = ollamaOptions.RequestTimeout
        };
        var draftModelPath = Path.Combine(WorkspaceStorage.EnsureDirectory(), "draft-model.json");
        var draftModelPreference = new JsonDraftModelPreferenceStore(draftModelPath);
        var draftProvider = new OllamaTranslationDraftProvider(httpClient, ollamaOptions, draftModelPreference);
        var draftModels = new DraftModelSettings(draftProvider, draftModelPreference, ollamaOptions.ModelName);
        var clock = new SystemClock();

        var getWorkQueue = new GetWorkQueueUseCase();
        var setTranslationDraft = new SetTranslationDraftUseCase();
        var mergeUnityCsvImport = new MergeUnityCsvImportUseCase();

        var session = new CatalogSession(
            projectFolderSettings,
            new CreateCatalogUseCase(),
            new ConfigureCatalogSettingsUseCase(),
            new OpenCatalogUseCase(store),
            new SaveCatalogUseCase(store),
            new ExportCatalogUseCase(exporter),
            new ImportUnityCsvUseCase(unityCsvReader, mergeUnityCsvImport),
            new ExportUnityCsvUseCase(unityCsvExporter));

        var mainViewModel = new MainViewModel(
            dialogs,
            session,
            draftModels,
            new AddCatalogEntryUseCase(),
            new UpdateCatalogEntryUseCase(),
            new RemoveCatalogEntryUseCase(),
            setTranslationDraft,
            new ApproveTranslationUseCase(),
            new InvalidateTranslationUseCase(),
            new RequestTranslationDraftsUseCase(draftProvider, clock, getWorkQueue, setTranslationDraft),
            new GetCatalogStatusSummaryUseCase(),
            new ValidateCatalogUseCase(),
            new FindAdjacentDraftUseCase(getWorkQueue));

        return (mainViewModel, dialogs);
    }

    private static OllamaDraftProviderOptions CreateOllamaOptions()
    {
        var baseUrl = Environment.GetEnvironmentVariable("LOCALIZER_OLLAMA_URL");
        var model = Environment.GetEnvironmentVariable("LOCALIZER_OLLAMA_MODEL");

        return new OllamaDraftProviderOptions
        {
            BaseAddress = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                ? uri
                : new Uri("http://127.0.0.1:11434/"),
            ModelName = string.IsNullOrWhiteSpace(model) ? null : model.Trim()
        };
    }
}
