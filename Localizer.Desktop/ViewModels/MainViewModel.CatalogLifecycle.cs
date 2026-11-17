using CommunityToolkit.Mvvm.Input;
using Localizer.Desktop.Services;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task NewCatalogAsync(CancellationToken cancellationToken)
    {
        if (!await ResolveUnsavedChangesAsync("creating a new catalog", cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        var request = await _dialogs.PromptCreateCatalogAsync().ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        var result = _session.Create(request);
        if (!result.Succeeded || result.Value is null)
        {
            await _dialogs.ShowMessageAsync("Create catalog failed", FormatError(result)).ConfigureAwait(true);
            return;
        }

        ShowCatalog(result.Value, dirty: true);
        await LoadProjectFolderBindingAsync(result.Value.CatalogId.Value).ConfigureAwait(true);
        StatusText = "Created new catalog.";
    }

    [RelayCommand]
    private async Task OpenCatalogAsync(CancellationToken cancellationToken)
    {
        if (!await ResolveUnsavedChangesAsync("opening another catalog", cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        var path = await _dialogs.PickOpenCatalogPathAsync(cancellationToken).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        await OpenCatalogFileAsync(path, cancellationToken).ConfigureAwait(true);
    }

    public async Task OpenCatalogFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await RunIoAsync(async token =>
        {
            var result = await _session.OpenAsync(path, token).ConfigureAwait(true);
            if (!result.Succeeded || result.Value is null)
            {
                await _dialogs.ShowMessageAsync("Open catalog failed", FormatError(result)).ConfigureAwait(true);
                return;
            }

            ShowCatalog(result.Value, dirty: false);
            await LoadProjectFolderBindingAsync(result.Value.CatalogId.Value).ConfigureAwait(true);
            StatusText = $"Opened {path}";
        }, cancellationToken).ConfigureAwait(true);
    }

    // Save only when there are unsaved changes and a known path (Save As handles first path).
    // Guards repeat CanSave because the keyboard shortcut bypasses the menu item's enabled state.
    [RelayCommand]
    private async Task SaveCatalogAsync(CancellationToken cancellationToken)
    {
        if (!CanSave || _session.Catalog is null || string.IsNullOrWhiteSpace(_session.Path))
        {
            return;
        }

        if (!await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        await SaveToPathAsync(_session.Path, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveCatalogAsAsync(CancellationToken cancellationToken)
    {
        if (!CanSaveAs || _session.Catalog is null || !await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var path = await _dialogs.PickSaveCatalogPathAsync(
            suggestedFileName: $"{_session.Catalog.CatalogId.Value}.json",
            cancellationToken).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        await SaveToPathAsync(path, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Asks what to do with unsaved changes before the window closes; false keeps it open.
    /// </summary>
    public Task<bool> ConfirmCloseAsync(CancellationToken cancellationToken = default) =>
        ResolveUnsavedChangesAsync("closing", cancellationToken);

    // True when the caller may replace or close the open catalog: nothing unsaved, changes
    // discarded on request, or changes saved first. Saving writes through the IO scope, which
    // would cancel a running operation, so unsaved changes during one block the action instead.
    private async Task<bool> ResolveUnsavedChangesAsync(string action, CancellationToken cancellationToken)
    {
        if (_session.Catalog is null || !HasUnsavedChanges)
        {
            return true;
        }

        if (IsBusy)
        {
            await _dialogs.ShowMessageAsync(
                "Operation in progress",
                $"Wait for the current operation to finish, or cancel it, before {action}.").ConfigureAwait(true);
            return false;
        }

        var name = _session.Path ?? $"{_session.Catalog.CatalogId.Value} (unsaved)";
        var choice = await _dialogs
            .PromptUnsavedChangesAsync($"Save changes to {name} before {action}?")
            .ConfigureAwait(true);

        return choice switch
        {
            UnsavedChangesChoice.Discard => true,
            UnsavedChangesChoice.Save => await SaveBeforeLeavingAsync(cancellationToken).ConfigureAwait(true),
            _ => false
        };
    }

    private async Task<bool> SaveBeforeLeavingAsync(CancellationToken cancellationToken)
    {
        if (_session.Catalog is null || !await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return false;
        }

        var path = _session.Path ?? await _dialogs.PickSaveCatalogPathAsync(
            suggestedFileName: $"{_session.Catalog.CatalogId.Value}.json",
            cancellationToken).ConfigureAwait(true);
        if (path is null)
        {
            return false;
        }

        await SaveToPathAsync(path, cancellationToken).ConfigureAwait(true);
        return !IsDirty;
    }

    [RelayCommand]
    private void CancelIo()
    {
        var ioCts = _ioCts;
        ioCts?.Cancel();
    }

    private async Task SaveToPathAsync(string path, CancellationToken cancellationToken)
    {
        await RunIoAsync(async token =>
        {
            var result = await _session.SaveAsync(path, token).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Save catalog failed", FormatError(result)).ConfigureAwait(true);
                return;
            }

            IsDirty = false;
            RefreshSaveCommandState();
            UpdateWindowTitle();
            StatusText = $"Saved {path}";
        }, cancellationToken).ConfigureAwait(true);
    }
}
