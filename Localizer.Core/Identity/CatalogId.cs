using Localizer.Core.Errors;

namespace Localizer.Core.Identity;

public sealed record CatalogId
{
    private CatalogId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CatalogId Create(string value)
    {
        if (!TryCreate(value, out var catalogId, out var error))
        {
            throw new DomainValidationException(error!.Code, error.Message);
        }

        return catalogId!;
    }

    public static bool TryCreate(string value, out CatalogId? catalogId, out DomainValidationException? error)
    {
        catalogId = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = new DomainValidationException(
                "catalog.id.invalid",
                "Catalog identifiers must be non-empty stable strings.");
            return false;
        }

        catalogId = new CatalogId(value.Trim());
        return true;
    }

    public override string ToString() => Value;
}
