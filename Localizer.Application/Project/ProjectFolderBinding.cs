namespace Localizer.Application.Project;

public sealed record ProjectFolderBinding
{
    public required string CatalogId { get; init; }

    public required string FolderPath { get; init; }

    public required string ImportFileName { get; init; }

    public required string ExportFileName { get; init; }

    public string CatalogFileName => $"{CatalogId}.json";

    public string ImportFilePath => Path.Combine(FolderPath, ImportFileName);

    public string ExportFilePath => Path.Combine(FolderPath, ExportFileName);

    public string CatalogFilePath => Path.Combine(FolderPath, CatalogFileName);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(FolderPath)
        && !string.IsNullOrWhiteSpace(ImportFileName)
        && !string.IsNullOrWhiteSpace(ExportFileName);
}
