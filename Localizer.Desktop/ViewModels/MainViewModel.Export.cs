using CommunityToolkit.Mvvm.Input;
using Localizer.Core.Validation;

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
        if (_catalog is null || !await CommitPendingEditsAsync().ConfigureAwait(true))
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
                ReplaceDiagnostics(outcome.Validation.Diagnostics);

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
        if (_catalog is null)
        {
            ReplaceDiagnostics([]);
            return;
        }

        ReplaceDiagnostics(_validateCatalog.Execute(_catalog).Diagnostics);
    }

    private void ReplaceDiagnostics(IEnumerable<ValidationDiagnostic> diagnostics)
    {
        Diagnostics.Clear();
        var errorCount = 0;
        var warningCount = 0;

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == ValidationSeverity.Error)
            {
                errorCount++;
            }
            else
            {
                warningCount++;
            }

            Diagnostics.Add(new DiagnosticRowViewModel(
                diagnostic.Severity.ToString(),
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.EntryKey?.Value,
                diagnostic.Locale?.Value));
        }

        DiagnosticsHeaderText = FormatDiagnosticsHeader(errorCount, warningCount);
    }

    private static string FormatDiagnosticsHeader(int errorCount, int warningCount)
    {
        if (errorCount == 0 && warningCount == 0)
        {
            return "Diagnostics: none";
        }

        var parts = new List<string>(2);
        if (errorCount > 0)
        {
            parts.Add(errorCount == 1 ? "1 error" : $"{errorCount} errors");
        }

        if (warningCount > 0)
        {
            parts.Add(warningCount == 1 ? "1 warning" : $"{warningCount} warnings");
        }

        return $"Diagnostics: {string.Join(", ", parts)}";
    }
}
