using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Localizer.Application.Import;
using Localizer.Application.Persistence;

namespace Localizer.Infrastructure.Import;

internal static partial class UnityCsvParser
{
    private const string KeyColumn = "Key";
    private const string IdColumn = "Id";

    public static UnityCsvDocument Parse(string csvText)
    {
        ArgumentNullException.ThrowIfNull(csvText);

        var rows = ParseRecords(csvText);
        if (rows.Count == 0)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.Malformed,
                "Unity CSV document is empty.");
        }

        var headers = rows[0];
        if (headers.Count < 3
            || !string.Equals(headers[0], KeyColumn, StringComparison.Ordinal)
            || !string.Equals(headers[1], IdColumn, StringComparison.Ordinal))
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.Malformed,
                "Unity CSV must start with Key,Id columns.");
        }

        var localeColumns = new List<(int Index, string LocaleCode, bool IsSource)>();
        for (var index = 2; index < headers.Count; index++)
        {
            if (!TryParseLocaleHeader(headers[index], out var localeCode, out var isSource))
            {
                throw new CatalogPersistenceException(
                    CatalogPersistenceErrorCodes.Malformed,
                    $"Unity CSV header '{headers[index]}' is not a recognized locale column.");
            }

            localeColumns.Add((index, localeCode, isSource));
        }

        var sourceColumns = localeColumns.Where(column => column.IsSource).ToList();
        if (sourceColumns.Count != 1)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.Malformed,
                "Unity CSV must contain exactly one source locale column.");
        }

        var sourceLocale = sourceColumns[0].LocaleCode;
        var targetLocales = localeColumns
            .Where(column => !column.IsSource)
            .Select(column => column.LocaleCode)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var sourceIndex = sourceColumns[0].Index;
        var parsedRows = new List<UnityCsvRow>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var fields = rows[rowIndex];
            if (fields.Count == 0 || fields.All(string.IsNullOrEmpty))
            {
                continue;
            }

            if (fields.Count < headers.Count)
            {
                fields = PadFields(fields, headers.Count);
            }

            var key = fields[0].Trim();
            if (string.IsNullOrEmpty(key))
            {
                throw new CatalogPersistenceException(
                    CatalogPersistenceErrorCodes.Malformed,
                    $"Unity CSV row {rowIndex + 1} is missing a key.");
            }

            if (!seenKeys.Add(key))
            {
                throw new CatalogPersistenceException(
                    CatalogPersistenceErrorCodes.Malformed,
                    $"Unity CSV contains duplicate key '{key}'.");
            }

            parsedRows.Add(new UnityCsvRow
            {
                Key = key,
                UnityId = fields[1].Trim(),
                SourceText = fields[sourceIndex]
            });
        }

        parsedRows.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));

        return new UnityCsvDocument
        {
            SourceLocale = sourceLocale,
            TargetLocales = targetLocales,
            Rows = parsedRows
        };
    }

    public static string FormatHeader(string localeCode, bool isSourceLocale)
    {
        var languageCode = localeCode.Split('-')[0];
        var displayName = LanguageDisplayNames.TryGetValue(languageCode, out var known)
            ? known
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(languageCode);
        _ = isSourceLocale;
        return $"{displayName}({localeCode})";
    }

    private static readonly Dictionary<string, string> LanguageDisplayNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "English",
            ["es"] = "Spanish",
            ["fi"] = "Finnish",
            ["fr"] = "French",
            ["de"] = "German",
            ["it"] = "Italian",
            ["pt"] = "Portuguese",
            ["ja"] = "Japanese",
            ["ko"] = "Korean",
            ["zh"] = "Chinese"
        };

    private static bool TryParseLocaleHeader(string header, out string localeCode, out bool isSource)
    {
        localeCode = string.Empty;
        isSource = false;

        var match = LocaleHeaderPattern().Match(header.Trim());
        if (!match.Success)
        {
            return false;
        }

        localeCode = match.Groups["code"].Value;
        isSource = string.Equals(localeCode, "en", StringComparison.OrdinalIgnoreCase)
                   || header.StartsWith("English(", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static List<string> PadFields(IReadOnlyList<string> fields, int count)
    {
        var padded = fields.ToList();
        while (padded.Count < count)
        {
            padded.Add(string.Empty);
        }

        return padded;
    }

    internal static List<List<string>> ParseRecords(string csvText)
    {
        var records = new List<List<string>>();
        var currentRecord = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < csvText.Length; index++)
        {
            var character = csvText[index];

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < csvText.Length && csvText[index + 1] == '"')
                    {
                        currentField.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    if (currentRecord.Count > 0)
                    {
                        records.Add(currentRecord);
                        currentRecord = [];
                    }
                    break;
                default:
                    currentField.Append(character);
                    break;
            }
        }

        if (currentField.Length > 0 || currentRecord.Count > 0)
        {
            currentRecord.Add(currentField.ToString());
            records.Add(currentRecord);
        }

        return records;
    }

    internal static string EscapeField(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    [GeneratedRegex(@"^(?<name>.+)\((?<code>[a-zA-Z]{2}(?:-[a-zA-Z0-9]+)?)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex LocaleHeaderPattern();
}
