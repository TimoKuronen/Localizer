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
        ITranslationDraftProvider draftProvider,
        DraftModelSettings? draftModels = null)
    {
        var getWorkQueue = new GetWorkQueueUseCase();
        var setTranslationDraft = new SetTranslationDraftUseCase();

        var session = new CatalogSession(
            projectFolderSettings,
            new CreateCatalogUseCase(),
            new ConfigureCatalogSettingsUseCase(),
            new OpenCatalogUseCase(store),
            new SaveCatalogUseCase(store),
            new ExportCatalogUseCase(new CompactLocaleJsonExporter()),
            new ImportStringTableCsvUseCase(new UnityCsvReader(), new MergeStringTableCsvImportUseCase()),
            new ExportStringTableCsvUseCase(new UnityCsvExporter()),
            new ImportSourceStringsUseCase(new SourceStringsJsonReader(), new MergeSourceStringsUseCase()),
            new ExportDraftPreviewUseCase(new DraftPreviewJsonWriter()),
            new FindEntriesNotInSourceUseCase(new SourceStringsJsonReader(), new UnityCsvReader()));

        return new MainViewModel(
            dialogs,
            session,
            draftModels ?? new DraftModelSettings(new FakeDraftModelCatalog([]), new InMemoryDraftModelPreference(), null),
            new AddCatalogEntryUseCase(),
            new UpdateCatalogEntryUseCase(),
            new RemoveCatalogEntryUseCase(),
            setTranslationDraft,
            new ApproveTranslationUseCase(),
            new ApproveStaleTranslationUseCase(),
            new FindMatchingTranslationsUseCase(),
            new ApproveMatchingTranslationsUseCase(),
            new InvalidateTranslationUseCase(),
            new RequestTranslationDraftsUseCase(
                draftProvider,
                new FixedClock(),
                getWorkQueue,
                setTranslationDraft),
            new GetCatalogStatusSummaryUseCase(),
            new ValidateCatalogUseCase(),
            new FindAdjacentReviewItemUseCase(getWorkQueue));
    }
}
