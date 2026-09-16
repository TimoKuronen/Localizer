using Localizer.Application.Export;
using Localizer.Application.Import;
using Localizer.Application.Persistence;
using Localizer.Application.Project;
using Localizer.Application.UseCases;
using Localizer.Desktop.Services;
using Localizer.Desktop.ViewModels;
using Localizer.Infrastructure.Export;
using Localizer.Infrastructure.Import;
using Localizer.Infrastructure.Persistence.Json;

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
            new ExportCatalogUseCase(exporter),
            new ImportUnityCsvUseCase(unityCsvReader, new MergeUnityCsvImportUseCase()),
            new ExportUnityCsvUseCase(unityCsvExporter),
            new GetCatalogStatusSummaryUseCase(),
            new ValidateCatalogUseCase());

        return (mainViewModel, dialogs);
    }
}
