using System.Text;
using Localizer.Application.UseCases;

namespace Localizer.Desktop.ViewModels;

public static class DraftPreviewReportText
{
    public static string Build(ExportDraftPreviewOutcome outcome, string directory)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var text = new StringBuilder();
        text.AppendLine($"Draft preview files written to {directory}");
        text.AppendLine("Preview only: these files include Draft and Stale text that is not approved.");
        text.AppendLine();

        foreach (var summary in outcome.Report.LocaleSummaries)
        {
            text.AppendLine(
                $"{summary.Locale}: {summary.IncludedCount} included "
                + $"(Approved {summary.ApprovedCount}, Draft {summary.DraftCount}, Stale {summary.StaleCount}); "
                + $"left out: Missing {summary.MissingCount}, Invalid {summary.InvalidCount}");
        }

        if (outcome.Report.SkippedItems.Count == 0)
        {
            return text.ToString().TrimEnd();
        }

        text.AppendLine();
        text.AppendLine("Left out:");
        foreach (var item in outcome.Report.SkippedItems)
        {
            var reason = item.Reason == DraftPreviewSkippedItem.SkipReason.Missing ? "Missing" : "Invalid";
            text.AppendLine($"  {item.Locale}  {item.Key}  [{reason}] {item.Message}");
        }

        return text.ToString().TrimEnd();
    }
}
