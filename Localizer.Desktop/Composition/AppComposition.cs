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
        IProjectFolderSettingsStore projectFolderSettings = new JsonProjectFolderSettingsStore();
        IUiDialogs dialogs = new AvaloniaUiDialogs();

        var ollamaOptions = CreateOllamaOptions();
        var httpClient = new HttpClient
        {
            BaseAddress = ollamaOptions.BaseAddress,
            Timeout = ollamaOptions.RequestTimeout
        };
        var draftProvider = new OllamaTranslationDraftProvider(httpClient, ollamaOptions);
        var clock = new SystemClock();

        var mainViewModel = new MainViewModel(
            dialogs,
            projectFolderSettings,
            new CreateCatalogUseCase(),
            new OpenCatalogUseCase(store),
            new SaveCatalogUseCase(store),
            new AddCatalogEntryUseCase(),
            new UpdateCatalogEntryUseCase(),
            new RemoveCatalogEntryUseCase(),
            new SetTranslationDraftUseCase(),
            new ApproveTranslationUseCase(),
            new RequestTranslationDraftsUseCase(draftProvider, clock),
            new ExportCatalogUseCase(exporter),
            new ImportUnityCsvUseCase(unityCsvReader, new MergeUnityCsvImportUseCase()),
            new ExportUnityCsvUseCase(unityCsvExporter),
            new GetCatalogStatusSummaryUseCase(),
            new ValidateCatalogUseCase());

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
            ModelName = string.IsNullOrWhiteSpace(model) ? "llama3.2" : model.Trim()
        };
    }
}
