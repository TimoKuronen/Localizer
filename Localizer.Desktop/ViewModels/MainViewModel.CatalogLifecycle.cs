using CommunityToolkit.Mvvm.Input;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task NewCatalogAsync()
    {
        var request = await _dialogs.PromptCreateCatalogAsync().ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        var result = _createCatalog.Execute(request);
        if (!result.Succeeded || result.Value is null)
        {
            await _dialogs.ShowMessageAsync("Create catalog failed", FormatError(result)).ConfigureAwait(true);
            return;
        }

        SetCatalog(result.Value, path: null, dirty: true);
        await LoadProjectFolderBindingAsync(result.Value.CatalogId.Value).ConfigureAwait(true);
        StatusText = "Created new catalog.";
    }

    [RelayCommand]
    private async Task OpenCatalogAsync(CancellationToken cancellationToken)
    {
        var path = await _dialogs.PickOpenCatalogPathAsync(cancellationToken).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        await RunIoAsync(async token =>
        {
            var result = await _openCatalog.ExecuteAsync(path, token).ConfigureAwait(true);
            if (!result.Succeeded || result.Value is null)
            {
                await _dialogs.ShowMessageAsync("Open catalog failed", FormatError(result)).ConfigureAwait(true);
                return;
            }

            SetCatalog(result.Value, path, dirty: false);
            await LoadProjectFolderBindingAsync(result.Value.CatalogId.Value).ConfigureAwait(true);
            StatusText = $"Opened {path}";
        }, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveCatalogAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null || string.IsNullOrWhiteSpace(_catalogPath) || !IsDirty)
        {
            return;
        }

        await SaveToPathAsync(_catalogPath, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveCatalogAsAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null)
        {
            return;
        }

        var path = await _dialogs.PickSaveCatalogPathAsync(
            suggestedFileName: $"{_catalog.CatalogId.Value}.json",
            cancellationToken).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        await SaveToPathAsync(path, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private void CancelIo() => _ioCts?.Cancel();

    private async Task SaveToPathAsync(string path, CancellationToken cancellationToken)
    {
        if (_catalog is null)
        {
            return;
        }

        await RunIoAsync(async token =>
        {
            var result = await _saveCatalog.ExecuteAsync(path, _catalog, token).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Save catalog failed", FormatError(result)).ConfigureAwait(true);
                return;
            }

            _catalogPath = path;
            IsDirty = false;
            RefreshSaveCommandState();
            UpdateWindowTitle();
            StatusText = $"Saved {path}";
        }, cancellationToken).ConfigureAwait(true);
    }
}
