using CommunityToolkit.Mvvm.ComponentModel;

namespace Localizer.Desktop.ViewModels;

public partial class ProjectFolderViewModel : ViewModelBase
{
    public const string SuggestedCsvFileName = "localization.csv";

    public ProjectFolderViewModel(string folderPath, string? importFileName, string? exportFileName)
    {
        FolderPath = folderPath;
        ImportFileName = string.IsNullOrWhiteSpace(importFileName) ? SuggestedCsvFileName : importFileName;
        ExportFileName = string.IsNullOrWhiteSpace(exportFileName) ? SuggestedCsvFileName : exportFileName;
    }

    public string FolderPath { get; }

    [ObservableProperty]
    public partial string ImportFileName { get; set; }

    [ObservableProperty]
    public partial string ExportFileName { get; set; }
}
