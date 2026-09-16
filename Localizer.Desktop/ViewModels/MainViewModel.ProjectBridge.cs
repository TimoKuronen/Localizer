using CommunityToolkit.Mvvm.Input;
using Localizer.Application.Project;
using Localizer.Application.UseCases;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task SetProjectFolderAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null)
        {
            return;
        }

        var folder = await _dialogs.PickExportDirectoryAsync(cancellationToken).ConfigureAwait(true);
        if (folder is null)
        {
            return;
        }

        var binding = await _dialogs
            .PromptProjectFolderAsync(folder, _projectFolderBinding)
            .ConfigureAwait(true);
        if (binding is null)
        {
            return;
        }

        binding = binding with { CatalogId = _catalog.CatalogId.Value };

        await _projectFolderSettings.SaveAsync(binding, cancellationToken).ConfigureAwait(true);
        ApplyProjectFolderBinding(binding);
        StatusText = $"Project folder set to {folder}.";
    }

    [RelayCommand]
    private async Task ImportFromProjectAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null || _projectFolderBinding is null || !_projectFolderBinding.IsConfigured)
        {
            return;
        }

        var importPath = _projectFolderBinding.ImportFilePath;
        if (!File.Exists(importPath))
        {
            await _dialogs.ShowMessageAsync(
                "Import failed",
                $"{UseCaseErrorCodes.ProjectFileNotFound}: Expected import file was not found at '{importPath}'.").ConfigureAwait(true);
            return;
        }

        await RunIoAsync(async token =>
        {
            var outcome = await _importUnityCsv.ExecuteAsync(_catalog, importPath, token).ConfigureAwait(true);
            if (!outcome.Succeeded)
            {
                await _dialogs.ShowMessageAsync(
                    "Import failed",
                    FormatImportError(outcome)).ConfigureAwait(true);
                return;
            }

            IsDirty = true;
            RefreshFromCatalog(selectKey: SelectedEntry?.Key);
            RunValidation();
            StatusText =
                $"Imported Unity CSV from {importPath}. Added {outcome.MergeOutcome.AddedCount}, updated {outcome.MergeOutcome.UpdatedCount}, unchanged {outcome.MergeOutcome.UnchangedCount}.";
        }, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExportToProjectAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null || _projectFolderBinding is null || !_projectFolderBinding.IsConfigured)
        {
            return;
        }

        var exportPath = _projectFolderBinding.ExportFilePath;

        await RunIoAsync(async token =>
        {
            var outcome = await _exportUnityCsv.ExecuteAsync(_catalog, exportPath, token).ConfigureAwait(true);
            if (!outcome.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Export failed", FormatUnityExportError(outcome)).ConfigureAwait(true);
                return;
            }

            StatusText = $"Exported Unity CSV to {exportPath}.";
            await _dialogs.ShowMessageAsync("Export complete", exportPath).ConfigureAwait(true);
        }, cancellationToken).ConfigureAwait(true);
    }

    private async Task LoadProjectFolderBindingAsync(string catalogId)
    {
        var binding = await _projectFolderSettings.GetAsync(catalogId).ConfigureAwait(true);
        ApplyProjectFolderBinding(binding);
    }

    private void ApplyProjectFolderBinding(ProjectFolderBinding? binding)
    {
        _projectFolderBinding = binding;
        HasProjectFolder = binding?.IsConfigured == true;
        ProjectFolderText = binding?.IsConfigured == true
            ? $"{binding.FolderPath} | import: {binding.ImportFileName} | export: {binding.ExportFileName}"
            : "Project folder not set.";
    }
}
