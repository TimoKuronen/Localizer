using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Syntax;

namespace Localizer.Application.UseCases;

public sealed record UpdateCatalogEntryRequest
{
    public required string Key { get; init; }

    public required string SourceText { get; init; }

    public string? Category { get; init; }

    public bool AllowEmptyText { get; init; }

    public string? DeveloperNotes { get; init; }

    public TranslationContext? Context { get; init; }

    public EntryConstraints? Constraints { get; init; }

    public MessageSyntaxProfile? SyntaxProfileOverride { get; init; }

    public bool ClearSyntaxProfileOverride { get; init; }

    public ExternalIds? ExternalIds { get; init; }
}

public sealed class UpdateCatalogEntryUseCase
{
    public UseCaseResult Execute(Catalog catalog, UpdateCatalogEntryRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);

        return DomainGuard.Try(() =>
        {
            var key = EntryKey.Create(request.Key);
            if (!catalog.Entries.TryGetValue(key, out var existing))
            {
                throw new Core.Errors.DomainValidationException(
                    UseCaseErrorCodes.EntryNotFound,
                    $"No entry with key '{request.Key}' exists.");
            }

            var syntaxOverride = request.ClearSyntaxProfileOverride
                ? null
                : request.SyntaxProfileOverride ?? existing.SyntaxProfileOverride;

            var updated = existing with
            {
                SourceText = request.SourceText,
                Category = request.Category,
                AllowEmptyText = request.AllowEmptyText,
                DeveloperNotes = request.DeveloperNotes,
                Context = request.Context ?? existing.Context,
                Constraints = request.Constraints ?? existing.Constraints,
                SyntaxProfileOverride = syntaxOverride,
                ExternalIds = request.ExternalIds ?? existing.ExternalIds
            };

            catalog.ReplaceEntry(updated);
        });
    }
}
