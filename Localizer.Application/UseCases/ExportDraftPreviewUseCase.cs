using Localizer.Application.Export;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;
using Localizer.Core.Lifecycle;
using Localizer.Core.Validation;

namespace Localizer.Application.UseCases;

public sealed record ExportDraftPreviewOutcome
{
    public required bool Succeeded { get; init; }

    public required DraftPreviewReport Report { get; init; }

    public required IReadOnlyList<string> WrittenFiles { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Writes a non-production locale file per required locale that also carries Draft and Stale text, so a
/// consumer can be exercised before every entry is approved. Missing entries and translations with blocking
/// validation errors are left out and listed in the report. It never changes the catalog.
/// </summary>
public sealed class ExportDraftPreviewUseCase
{
    private readonly IDraftPreviewWriter _writer;

    public ExportDraftPreviewUseCase(IDraftPreviewWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public async Task<ExportDraftPreviewOutcome> ExecuteAsync(
        Catalog catalog,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var (files, report) = Collect(catalog);

        try
        {
            var writtenFiles = await _writer
                .WriteAsync(outputDirectory, files, cancellationToken)
                .ConfigureAwait(false);

            return new ExportDraftPreviewOutcome
            {
                Succeeded = true,
                Report = report,
                WrittenFiles = writtenFiles
            };
        }
        catch (CatalogPersistenceException exception)
        {
            return Failure(report, exception.Code, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Failure(report, UseCaseErrorCodes.InvalidArgument, exception.Message);
        }
    }

    private static (IReadOnlyList<LocaleTexts> Files, DraftPreviewReport Report) Collect(Catalog catalog)
    {
        var entries = catalog.Entries.Values
            .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
            .ToList();
        var files = new List<LocaleTexts>();
        var summaries = new List<DraftPreviewLocaleSummary>();
        var skipped = new List<DraftPreviewSkippedItem>();

        foreach (var locale in catalog.RequiredLocales.OrderBy(locale => locale.Value, StringComparer.Ordinal))
        {
            var texts = new List<KeyValuePair<string, string>>();
            var approved = 0;
            var draft = 0;
            var stale = 0;
            var missing = 0;
            var invalid = 0;

            foreach (var entry in entries)
            {
                var status = catalog.GetEffectiveStatus(entry, locale);
                if (status == TranslationEffectiveStatus.Missing)
                {
                    missing++;
                    skipped.Add(new DraftPreviewSkippedItem
                    {
                        Key = entry.Key.Value,
                        Locale = locale.Value,
                        Reason = DraftPreviewSkippedItem.SkipReason.Missing,
                        Message = "No translation yet."
                    });
                    continue;
                }

                var blocking = CatalogValidator.ValidateTranslation(catalog, entry, locale)
                    .Diagnostics
                    .FirstOrDefault(diagnostic => diagnostic.Severity == ValidationSeverity.Error);
                if (blocking is not null)
                {
                    invalid++;
                    skipped.Add(new DraftPreviewSkippedItem
                    {
                        Key = entry.Key.Value,
                        Locale = locale.Value,
                        Reason = DraftPreviewSkippedItem.SkipReason.InvalidTranslation,
                        Message = $"{blocking.Code}: {blocking.Message}"
                    });
                    continue;
                }

                texts.Add(new KeyValuePair<string, string>(entry.Key.Value, entry.Translations[locale].Text));
                switch (status)
                {
                    case TranslationEffectiveStatus.Approved:
                        approved++;
                        break;
                    case TranslationEffectiveStatus.Stale:
                        stale++;
                        break;
                    default:
                        draft++;
                        break;
                }
            }

            files.Add(new LocaleTexts { Locale = locale, Texts = texts });
            summaries.Add(new DraftPreviewLocaleSummary
            {
                Locale = locale.Value,
                ApprovedCount = approved,
                DraftCount = draft,
                StaleCount = stale,
                MissingCount = missing,
                InvalidCount = invalid
            });
        }

        return (files, new DraftPreviewReport { LocaleSummaries = summaries, SkippedItems = skipped });
    }

    private static ExportDraftPreviewOutcome Failure(DraftPreviewReport report, string code, string message) =>
        new()
        {
            Succeeded = false,
            Report = report,
            WrittenFiles = [],
            ErrorCode = code,
            ErrorMessage = message
        };
}
