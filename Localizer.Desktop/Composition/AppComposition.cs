using Localizer.Application.Persistence;
using Localizer.Application.UseCases;
using Localizer.Desktop.Services;
using Localizer.Desktop.ViewModels;
using Localizer.Infrastructure.Persistence.Json;

namespace Localizer.Desktop.Composition;

public static class AppComposition
{
    public static (MainViewModel MainViewModel, IUiDialogs Dialogs) Create()
    {
        ICatalogStore store = new JsonCatalogStore();
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
            new GetCatalogStatusSummaryUseCase(),
            new ValidateCatalogUseCase());

        return (mainViewModel, dialogs);
    }
}
