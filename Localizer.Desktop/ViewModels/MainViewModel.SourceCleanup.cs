using CommunityToolkit.Mvvm.Input;
using Localizer.Application.UseCases;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task RemoveEntriesNotInSourceAsync(CancellationToken cancellationToken)
    {
        if (_session.Catalog is not { } catalog || !await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var path = await PickComparisonPathAsync(cancellationToken).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        IReadOnlyList<SourceMissingEntry>? missing = null;
        await RunIoAsync(async token =>
        {
            var result = await _session.FindEntriesNotInSourceAsync(path, token).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                await _dialogs
                    .ShowMessageAsync("Compare failed", $"{result.ErrorCode}: {result.ErrorMessage}")
                    .ConfigureAwait(true);
                return;
            }

            missing = result.Value;
        }, cancellationToken).ConfigureAwait(true);

        if (missing is null)
        {
            return;
        }

        if (missing.Count == 0)
        {
            StatusText = $"Every catalog entry is listed in {path}.";
            return;
        }

        var selected = await _dialogs.PromptChecklistAsync(CreateRemovalChecklist(path, missing)).ConfigureAwait(true);
        if (selected is null || selected.Count == 0)
        {
            StatusText = "No entries removed.";
            return;
        }

        foreach (var key in selected)
        {
            var removed = _removeEntry.Execute(catalog, key);
            if (!removed.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Remove entry failed", UseCaseErrorText.Format(removed)).ConfigureAwait(true);
                break;
            }

            IsDirty = true;
        }

        RefreshFromCatalog(selectKey: SelectedEntry?.Key);
        RunValidation();
        StatusText = $"Removed {selected.Count} entries not listed in the source. Save to keep the change.";
    }

    // The bound project file is the source when a folder is set; its absence is an error, not a prompt.
    private async Task<string?> PickComparisonPathAsync(CancellationToken cancellationToken)
    {
        if (_session.ProjectFolderBinding is not { IsConfigured: true } binding)
        {
            return await _dialogs.PickComparisonSourcePathAsync(cancellationToken).ConfigureAwait(true);
        }

        if (File.Exists(binding.ImportFilePath))
        {
            return binding.ImportFilePath;
        }

        await _dialogs.ShowMessageAsync(
            "Compare failed",
            $"{UseCaseErrorCodes.ProjectFileNotFound}: Expected import file was not found at '{binding.ImportFilePath}'.")
            .ConfigureAwait(true);
        return null;
    }

    private static ChecklistViewModel CreateRemovalChecklist(string path, IReadOnlyList<SourceMissingEntry> missing)
    {
        var approved = missing.Count(entry => entry.HasApprovedText);
        var items = missing.Select(entry =>
        {
            var statuses = string.Join(" | ", entry.LocaleStatuses.Select(item => $"{item.Locale}:{item.Status}"));
            var marker = entry.HasApprovedText ? "Has approved text. " : string.Empty;
            return new ChecklistItemViewModel(
                entry.Key,
                $"{marker}{statuses}{Environment.NewLine}{entry.SourceText}",
                isSelected: true);
        }).ToList();

        return new ChecklistViewModel(
            "Remove entries not in source",
            $"{missing.Count} entries are not listed in {path}; {approved} of them have approved text. "
            + "Removing an entry also removes its translations. Untick any entry to keep it.",
            "Remove",
            "Cancel",
            confirmIsDefault: false,
            items);
    }
}
