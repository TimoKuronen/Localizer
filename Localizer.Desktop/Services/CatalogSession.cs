using Localizer.Application.Project;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;

namespace Localizer.Desktop.Services;

/// <summary>
/// The open catalog, its file path and its project folder binding, together with the
/// use cases that create, load, persist and exchange that catalog. Presentation state
/// (dirty flag, selection, status text) stays with the view model.
/// </summary>
public sealed class CatalogSession
{
    private readonly IProjectFolderSettingsStore _projectFolderSettings;
    private readonly CreateCatalogUseCase _createCatalog;
    private readonly ConfigureCatalogSettingsUseCase _configureSettings;
    private readonly OpenCatalogUseCase _openCatalog;
    private readonly SaveCatalogUseCase _saveCatalog;
    private readonly ExportCatalogUseCase _exportCatalog;
    private readonly ImportStringTableCsvUseCase _importStringTableCsv;
    private readonly ExportStringTableCsvUseCase _exportStringTableCsv;
    private readonly ImportSourceStringsUseCase _importSourceStrings;
    private readonly ExportDraftPreviewUseCase _exportDraftPreview;
    private readonly FindEntriesNotInSourceUseCase _findEntriesNotInSource;

    public CatalogSession(
        IProjectFolderSettingsStore projectFolderSettings,
        CreateCatalogUseCase createCatalog,
        ConfigureCatalogSettingsUseCase configureSettings,
        OpenCatalogUseCase openCatalog,
        SaveCatalogUseCase saveCatalog,
        ExportCatalogUseCase exportCatalog,
        ImportStringTableCsvUseCase importStringTableCsv,
        ExportStringTableCsvUseCase exportStringTableCsv,
        ImportSourceStringsUseCase importSourceStrings,
        ExportDraftPreviewUseCase exportDraftPreview,
        FindEntriesNotInSourceUseCase findEntriesNotInSource)
    {
        _projectFolderSettings = projectFolderSettings;
        _createCatalog = createCatalog;
        _configureSettings = configureSettings;
        _openCatalog = openCatalog;
        _saveCatalog = saveCatalog;
        _exportCatalog = exportCatalog;
        _importStringTableCsv = importStringTableCsv;
        _exportStringTableCsv = exportStringTableCsv;
        _importSourceStrings = importSourceStrings;
        _exportDraftPreview = exportDraftPreview;
        _findEntriesNotInSource = findEntriesNotInSource;
    }

    public Catalog? Catalog { get; private set; }

    /// <summary>
    /// File the catalog was last opened from or saved to; null until the first write.
    /// </summary>
    public string? Path { get; private set; }

    public ProjectFolderBinding? ProjectFolderBinding { get; private set; }

    public UseCaseResult<Catalog> Create(CreateCatalogRequest request)
    {
        var result = _createCatalog.Execute(request);
        if (result.Succeeded && result.Value is not null)
        {
            Attach(result.Value, path: null);
        }

        return result;
    }

    public UseCaseResult ConfigureSettings(ConfigureCatalogSettingsRequest request) =>
        _configureSettings.Execute(RequireCatalog(), request);

    public async Task<UseCaseResult<Catalog>> OpenAsync(string path, CancellationToken cancellationToken)
    {
        var result = await _openCatalog.ExecuteAsync(path, cancellationToken).ConfigureAwait(true);
        if (result.Succeeded && result.Value is not null)
        {
            Attach(result.Value, path);
        }

        return result;
    }

    public async Task<UseCaseResult> SaveAsync(string path, CancellationToken cancellationToken)
    {
        var result = await _saveCatalog.ExecuteAsync(path, RequireCatalog(), cancellationToken).ConfigureAwait(true);
        if (result.Succeeded)
        {
            Path = path;
        }

        return result;
    }

    public Task<ExportCatalogOutcome> ExportAsync(string outputDirectory, CancellationToken cancellationToken) =>
        _exportCatalog.ExecuteAsync(RequireCatalog(), outputDirectory, cancellationToken);

    public Task<ImportStringTableCsvOutcome> ImportStringTableCsvAsync(string path, CancellationToken cancellationToken) =>
        _importStringTableCsv.ExecuteAsync(RequireCatalog(), path, cancellationToken);

    public Task<SourceStringsImportOutcome> ImportSourceStringsAsync(string path, CancellationToken cancellationToken) =>
        _importSourceStrings.ExecuteAsync(RequireCatalog(), path, cancellationToken);

    /// <summary>
    /// A .csv path is read as a string table CSV, any other path as source strings JSON.
    /// </summary>
    public Task<UseCaseResult<IReadOnlyList<SourceMissingEntry>>> FindEntriesNotInSourceAsync(
        string path,
        CancellationToken cancellationToken) =>
        string.Equals(System.IO.Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)
            ? _findEntriesNotInSource.FromStringTableCsvAsync(RequireCatalog(), path, cancellationToken)
            : _findEntriesNotInSource.FromSourceStringsAsync(RequireCatalog(), path, cancellationToken);

    public Task<ExportDraftPreviewOutcome> ExportDraftPreviewAsync(string outputDirectory, CancellationToken cancellationToken) =>
        _exportDraftPreview.ExecuteAsync(RequireCatalog(), outputDirectory, cancellationToken);

    public Task<ExportStringTableCsvOutcome> ExportStringTableCsvAsync(string path, CancellationToken cancellationToken) =>
        _exportStringTableCsv.ExecuteAsync(RequireCatalog(), path, cancellationToken);

    public async Task<ProjectFolderBinding?> LoadProjectFolderBindingAsync(string catalogId)
    {
        ProjectFolderBinding = await _projectFolderSettings.GetAsync(catalogId).ConfigureAwait(true);
        return ProjectFolderBinding;
    }

    public async Task SaveProjectFolderBindingAsync(ProjectFolderBinding binding, CancellationToken cancellationToken)
    {
        await _projectFolderSettings.SaveAsync(binding, cancellationToken).ConfigureAwait(true);
        ProjectFolderBinding = binding;
    }

    private void Attach(Catalog catalog, string? path)
    {
        Catalog = catalog;
        Path = path;
    }

    private Catalog RequireCatalog() =>
        Catalog ?? throw new InvalidOperationException("No catalog is open.");
}
