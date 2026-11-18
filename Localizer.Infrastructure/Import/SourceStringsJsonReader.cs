using System.Text.Json;
using System.Text.Json.Serialization;
using Localizer.Application.Import;
using Localizer.Application.Persistence;
using Localizer.Core.Catalogs;
using Localizer.Core.Errors;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;

namespace Localizer.Infrastructure.Import;

public sealed class SourceStringsJsonReader : ISourceStringsReader
{
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<SourceStringsDocument> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.IoFailed,
                $"Failed to read source strings file '{path}'.",
                exception);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.ByteOrderMarkPresent,
                "Source strings file must be UTF-8 without a byte order mark.");
        }

        return Map(Deserialize(bytes));
    }

    private static SourceStringsDocumentDto Deserialize(byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<SourceStringsDocumentDto>(bytes, SerializerOptions)
                   ?? throw Malformed("Source strings document was empty.");
        }
        catch (JsonException exception)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.Malformed,
                $"Source strings document is malformed: {exception.Message}",
                exception);
        }
    }

    private static SourceStringsDocument Map(SourceStringsDocumentDto dto)
    {
        if (dto.SchemaVersion == 0)
        {
            throw Malformed("Source strings document requires schemaVersion.");
        }

        if (dto.SchemaVersion != SupportedSchemaVersion)
        {
            throw new CatalogPersistenceException(
                CatalogPersistenceErrorCodes.UnsupportedVersion,
                $"Source strings schemaVersion {dto.SchemaVersion} is not supported.");
        }

        if (!Locale.TryCreate(dto.SourceLocale ?? string.Empty, out var sourceLocale, out _))
        {
            throw Malformed("Source strings document requires a valid BCP 47 sourceLocale.");
        }

        if (dto.Entries is null)
        {
            throw Malformed("Source strings document requires an entries array.");
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<SourceStringRow>(dto.Entries.Count);
        foreach (var entry in dto.Entries)
        {
            var row = MapEntry(entry);
            if (!seenKeys.Add(row.Key))
            {
                throw Malformed($"Source strings document contains duplicate key '{row.Key}'.");
            }

            rows.Add(row);
        }

        return new SourceStringsDocument
        {
            SourceLocale = sourceLocale!.Value,
            Rows = rows.OrderBy(row => row.Key, StringComparer.Ordinal).ToList()
        };
    }

    private static SourceStringRow MapEntry(SourceStringEntryDto entry)
    {
        if (!EntryKey.TryCreate(entry.Key ?? string.Empty, out var key, out var keyError))
        {
            throw Malformed($"Entry key '{entry.Key}' is invalid: {keyError!.Message}");
        }

        if (entry.SourceText is null)
        {
            throw Malformed($"Entry '{key!.Value}' requires sourceText.");
        }

        return new SourceStringRow
        {
            Key = key!.Value,
            SourceText = entry.SourceText,
            Category = entry.Category,
            AllowEmptyText = entry.AllowEmptyText,
            DeveloperNotes = entry.DeveloperNotes,
            Context = entry.Context,
            Constraints = MapConstraints(entry.Constraints),
            SyntaxProfile = MapSyntaxProfile(key.Value, entry.SyntaxProfile),
            ExternalIds = entry.ExternalIds
        };
    }

    private static EntryConstraints? MapConstraints(Persistence.Json.EntryConstraintsDto? dto) =>
        dto is null
            ? null
            : new EntryConstraints
            {
                MaxGraphemes = dto.MaxGraphemes,
                MaxUtf8Bytes = dto.MaxUtf8Bytes,
                MaxLines = dto.MaxLines,
                RequiredTerms = dto.RequiredTerms?.OrderBy(term => term, StringComparer.Ordinal).ToList() ?? [],
                ForbiddenTerms = dto.ForbiddenTerms?.OrderBy(term => term, StringComparer.Ordinal).ToList() ?? []
            };

    private static MessageSyntaxProfile? MapSyntaxProfile(string key, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            return MessageSyntaxProfileNames.FromName(name);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw Malformed($"Entry '{key}' has unsupported syntaxProfile '{name}'.");
        }
    }

    private static CatalogPersistenceException Malformed(string message) =>
        new(CatalogPersistenceErrorCodes.Malformed, message);
}
