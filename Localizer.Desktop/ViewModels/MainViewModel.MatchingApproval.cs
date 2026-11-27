using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    // Offered after a single approval so one review decision can cover every repeat of the source.
    private async Task OfferMatchingApprovalsAsync(Catalog catalog, string key, string locale)
    {
        var matches = _findMatchingTranslations.Execute(catalog, key, locale);
        if (matches.Count == 0)
        {
            return;
        }

        var text = catalog.Entries[Core.Identity.EntryKey.Create(key)]
            .Translations[Core.Identity.Locale.Create(locale)]
            .Text;
        var selected = await _dialogs
            .PromptChecklistAsync(CreateMatchingChecklist(key, locale, text, matches))
            .ConfigureAwait(true);
        if (selected is null || selected.Count == 0)
        {
            return;
        }

        var result = _approveMatchingTranslations.Execute(catalog, new ApproveMatchingTranslationsRequest
        {
            SourceKey = key,
            Locale = locale,
            TargetKeys = selected
        });
        if (!result.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Approve matching failed", $"{result.ErrorCode}: {result.ErrorMessage}")
                .ConfigureAwait(true);
            return;
        }

        var outcome = result.Value!;
        RefreshEditorInPlace();
        SelectReviewTarget(locale);
        StatusText = $"Approved '{key}' for {locale} and {outcome.ApprovedKeys.Count} matching entries.";

        if (outcome.BlockedMessages.Count > 0)
        {
            await _dialogs.ShowReportAsync(
                "Matching entries left unchanged",
                string.Join(Environment.NewLine, outcome.BlockedMessages)).ConfigureAwait(true);
        }
    }

    private static ChecklistViewModel CreateMatchingChecklist(
        string key,
        string locale,
        string text,
        IReadOnlyList<MatchingTranslation> matches)
    {
        var items = matches.Select(match =>
        {
            var lines = new List<string>
            {
                match.WasApproved ? $"{match.Status}, approved earlier" : match.Status.ToString(),
                $"Current text: {match.CurrentText ?? "(none)"}"
            };
            if (!match.IsIdentical)
            {
                lines.Add("Developer notes, context or constraints differ.");
                lines.Add($"Developer notes: {match.DeveloperNotes ?? "(none)"}");
            }

            // Differing details or earlier approved text need a deliberate tick.
            return new ChecklistItemViewModel(
                match.Key,
                string.Join(Environment.NewLine, lines),
                isSelected: match.IsIdentical && !match.WasApproved);
        }).ToList();

        return new ChecklistViewModel(
            "Approve matching translations",
            $"These entries have the same source as '{key}'. Ticked entries get this {locale} text and are approved: {text}",
            "Approve",
            "Skip",
            confirmIsDefault: true,
            items);
    }
}
