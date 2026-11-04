using CommunityToolkit.Mvvm.ComponentModel;

namespace Localizer.Desktop.ViewModels;

public partial class ProjectFolderViewModel : ViewModelBase
{
    public ProjectFolderViewModel(
        string folderPath,
        IReadOnlyList<string> csvFileNames,
        string? importFileName,
        string? exportFileName)
    {
        FolderPath = folderPath;
        CsvFileNames = csvFileNames;
        ScanSummaryText = csvFileNames.Count switch
        {
            0 => "No CSV files found in this folder. Type the import file name.",
            1 => "Found 1 CSV file in this folder.",
            _ => $"Found {csvFileNames.Count} CSV files in this folder."
        };

        // A sole CSV in the folder is almost always the Unity export to round-trip.
        ImportFileName = !string.IsNullOrWhiteSpace(importFileName)
            ? importFileName
            : csvFileNames.Count == 1 ? csvFileNames[0] : string.Empty;

        // Unity CSV Merge re-imports the file it exported, so new bindings mirror by default.
        MirrorExport = string.IsNullOrWhiteSpace(exportFileName)
            || string.Equals(exportFileName, ImportFileName, StringComparison.OrdinalIgnoreCase);
        ExportFileName = MirrorExport ? ImportFileName : exportFileName!;
    }

    public string FolderPath { get; }

    public IReadOnlyList<string> CsvFileNames { get; }

    public string ScanSummaryText { get; }

    public bool CanSave =>
        !string.IsNullOrWhiteSpace(ImportFileName)
        && !string.IsNullOrWhiteSpace(ExportFileName);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string ImportFileName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string ExportFileName { get; set; }

    [ObservableProperty]
    public partial bool MirrorExport { get; set; }

    partial void OnImportFileNameChanged(string value)
    {
        if (MirrorExport)
        {
            ExportFileName = value;
        }
    }

    partial void OnMirrorExportChanged(bool value)
    {
        if (value)
        {
            ExportFileName = ImportFileName;
        }
    }
}
