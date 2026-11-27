using Localizer.Application.Import;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;
using Localizer.Core.Errors;
using Localizer.Core.Lifecycle;

namespace Localizer.Application.UseCases;

/// <summary>
/// Lists catalog entries whose keys are absent from a source file, compared ordinally. The file is
/// taken to hold every key of the catalog, and the catalog is not changed.
/// </summary>
public sealed class FindEntriesNotInSourceUseCase
{
    private readonly ISourceStringsReader _sourceStrings;
    private readonly IStringTableCsvReader _stringTableCsv;

    public FindEntriesNotInSourceUseCase(ISourceStringsReader sourceStrings, IStringTableCsvReader stringTableCsv)
    {
        _sourceStrings = sourceStrings ?? throw new ArgumentNullException(nameof(sourceStrings));
        _stringTableCsv = stringTableCsv ?? throw new ArgumentNullException(nameof(stringTableCsv));
    }

    public Task<UseCaseResult<IReadOnlyList<SourceMissingEntry>>> FromSourceStringsAsync(
        Catalog catalog,
        string path,
        CancellationToken cancellationToken = default) =>
        CompareAsync(catalog, path, async () =>
        {
            var document = await _sourceStrings.ReadAsync(path, cancellationToken).ConfigureAwait(false);
            return (document.SourceLocale, document.Rows.Select(row => row.Key).ToList());
        });

    public Task<UseCaseResult<IReadOnlyList<SourceMissingEntry>>> FromStringTableCsvAsync(
        Catalog catalog,
        string path,
        CancellationToken cancellationToken = default) =>
        CompareAsync(catalog, path, async () =>
        {
            var document = await _stringTableCsv
                .ReadAsync(path, catalog.SourceLocale.Value, cancellationToken)
                .ConfigureAwait(false);
            return (document.SourceLocale, document.Rows.Select(row => row.Key).ToList());
        });

    private static async Task<UseCaseResult<IReadOnlyList<SourceMissingEntry>>> CompareAsync(
        Catalog catalog,
        string path,
        Func<Task<(string SourceLocale, List<string> Keys)>> readAsync)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var (sourceLocale, keys) = await readAsync().ConfigureAwait(false);
            if (!string.Equals(sourceLocale, catalog.SourceLocale.Value, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    UseCaseErrorCodes.ImportLocaleMismatch,
                    $"Source file locale '{sourceLocale}' does not match catalog source locale '{catalog.SourceLocale.Value}'.");
            }

            // An empty file would mark the whole catalog for removal.
            if (keys.Count == 0)
            {
                return Failure(UseCaseErrorCodes.ImportSourceEmpty, "The source file lists no keys.");
            }

            var present = new HashSet<string>(keys, StringComparer.Ordinal);
            IReadOnlyList<SourceMissingEntry> missing = catalog.Entries.Values
                .Where(entry => !present.Contains(entry.Key.Value))
                .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
                .Select(entry => Describe(catalog, entry))
                .ToList();

            return UseCaseResult<IReadOnlyList<SourceMissingEntry>>.Success(missing);
        }
        catch (CatalogPersistenceException exception)
        {
            return Failure(exception.Code, exception.Message);
        }
        catch (DomainValidationException exception)
        {
            return Failure(exception.Code, exception.Message);
        }
    }

    private static SourceMissingEntry Describe(Catalog catalog, CatalogEntry entry) =>
        new()
        {
            Key = entry.Key.Value,
            SourceText = entry.SourceText,
            LocaleStatuses = catalog.RequiredLocales
                .OrderBy(locale => locale.Value, StringComparer.Ordinal)
                .Select(locale => (locale.Value, catalog.GetEffectiveStatus(entry, locale)))
                .ToList(),
            HasApprovedText = entry.Translations.Values.Any(translation => translation.State == TranslationState.Approved)
        };

    private static UseCaseResult<IReadOnlyList<SourceMissingEntry>> Failure(string code, string message) =>
        UseCaseResult<IReadOnlyList<SourceMissingEntry>>.Failure(code, message);
}
