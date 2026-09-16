using CommunityToolkit.Mvvm.Input;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void ValidateCatalog()
    {
        if (_catalog is null)
        {
            return;
        }

        RunValidation();
        StatusText = Diagnostics.Count == 0
            ? "Validation found no diagnostics."
            : $"Validation reported {Diagnostics.Count} diagnostic(s).";
    }

    [RelayCommand]
    private async Task ExportCatalogAsync(CancellationToken cancellationToken)
    {
        if (_catalog is null)
        {
            return;
        }

        var directory = await _dialogs.PickExportDirectoryAsync(cancellationToken).ConfigureAwait(true);
        if (directory is null)
        {
            return;
        }

        await RunIoAsync(async token =>
        {
            var outcome = await _exportCatalog.ExecuteAsync(_catalog, directory, token).ConfigureAwait(true);
            if (!outcome.Succeeded)
            {
                Diagnostics.Clear();
                foreach (var diagnostic in outcome.Validation.Diagnostics)
                {
                    Diagnostics.Add(new DiagnosticRowViewModel(
                        diagnostic.Severity.ToString(),
                        diagnostic.Code,
                        diagnostic.Message,
                        diagnostic.EntryKey?.Value,
                        diagnostic.Locale?.Value));
                }

                var message = string.IsNullOrWhiteSpace(outcome.ErrorCode)
                    ? outcome.ErrorMessage ?? "Export failed."
                    : $"{outcome.ErrorCode}: {outcome.ErrorMessage}";
                if (outcome.Validation.Diagnostics.Count > 0)
                {
                    message += $"{Environment.NewLine}{Environment.NewLine}See diagnostics panel for {outcome.Validation.Diagnostics.Count} finding(s).";
                }

                await _dialogs.ShowMessageAsync("Export failed", message).ConfigureAwait(true);
                StatusText = "Export blocked.";
                return;
            }

            StatusText = $"Exported {outcome.WrittenFiles.Count} locale file(s) to {directory}.";
            await _dialogs.ShowMessageAsync(
                "Export complete",
                string.Join(Environment.NewLine, outcome.WrittenFiles)).ConfigureAwait(true);
        }, cancellationToken).ConfigureAwait(true);
    }

    private void RunValidation()
    {
        Diagnostics.Clear();
        if (_catalog is null)
        {
            return;
        }

        var result = _validateCatalog.Execute(_catalog);
        foreach (var diagnostic in result.Diagnostics)
        {
            Diagnostics.Add(new DiagnosticRowViewModel(
                diagnostic.Severity.ToString(),
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.EntryKey?.Value,
                diagnostic.Locale?.Value));
        }
    }
}
