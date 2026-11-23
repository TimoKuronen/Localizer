using Localizer.Application.UseCases;

namespace Localizer.Desktop.ViewModels;

internal static class UseCaseErrorText
{
    public static string Format(UseCaseResult result) =>
        Format(result.ErrorCode, result.ErrorMessage, "Unknown error.");

    public static string Format<T>(UseCaseResult<T> result) =>
        Format(result.ErrorCode, result.ErrorMessage, "Unknown error.");

    public static string Format(ImportStringTableCsvOutcome outcome) =>
        Format(outcome.ErrorCode, outcome.ErrorMessage, "Import failed.");

    public static string Format(ExportStringTableCsvOutcome outcome) =>
        Format(outcome.ErrorCode, outcome.ErrorMessage, "Export failed.");

    private static string Format(string? code, string? message, string fallback) =>
        string.IsNullOrWhiteSpace(code) ? message ?? fallback : $"{code}: {message}";
}
