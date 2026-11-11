using Localizer.Application.Drafting;
using Localizer.Application.Persistence;
using Localizer.Application.Project;
using Localizer.Application.UseCases;
using Localizer.Desktop.Services;
using Localizer.Desktop.ViewModels;
using Localizer.Infrastructure.Export;
using Localizer.Infrastructure.Import;

namespace Localizer.Desktop.Tests.Fakes;

internal static class MainViewModelFactory
{
    public static MainViewModel Create(
        IUiDialogs dialogs,
        ICatalogStore store,
        IProjectFolderSettingsStore projectFolderSettings,
        ITranslationDraftProvider draftProvider)
    {
        var getWorkQueue = new GetWorkQueueUseCase();
        var setTranslationDraft = new SetTranslationDraftUseCase();

        return new MainViewModel(
            dialogs,
            projectFolderSettings,
            new CreateCatalogUseCase(),
            new OpenCatalogUseCase(store),
            new SaveCatalogUseCase(store),
            new AddCatalogEntryUseCase(),
            new UpdateCatalogEntryUseCase(),
            new RemoveCatalogEntryUseCase(),
            setTranslationDraft,
            new ApproveTranslationUseCase(),
            new InvalidateTranslationUseCase(),
            new RequestTranslationDraftsUseCase(
                draftProvider,
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
}
