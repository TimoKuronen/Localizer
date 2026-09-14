using Localizer.Application.Export;
using Localizer.Application.Persistence;
using Localizer.Application.UseCases;
using Localizer.Desktop.Services;
using Localizer.Desktop.ViewModels;
using Localizer.Infrastructure.Export;
using Localizer.Infrastructure.Persistence.Json;

namespace Localizer.Desktop.Composition;

public static class AppComposition
{
    public static (MainViewModel MainViewModel, IUiDialogs Dialogs) Create()
    {
        ICatalogStore store = new JsonCatalogStore();
        ICatalogExporter exporter = new CompactLocaleJsonExporter();
        IUiDialogs dialogs = new AvaloniaUiDialogs();

        var mainViewModel = new MainViewModel(
            dialogs,
            new CreateCatalogUseCase(),
            new OpenCatalogUseCase(store),
            new SaveCatalogUseCase(store),
            new AddCatalogEntryUseCase(),
            new UpdateCatalogEntryUseCase(),
            new RemoveCatalogEntryUseCase(),
            new SetTranslationDraftUseCase(),
            new ApproveTranslationUseCase(),
            new ExportCatalogUseCase(exporter),
            new GetCatalogStatusSummaryUseCase(),
            new ValidateCatalogUseCase());

        return (mainViewModel, dialogs);
    }
}
