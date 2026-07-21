namespace Localizer.Application.Persistence;

public sealed class CatalogPersistenceException : Exception
{
    public CatalogPersistenceException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public static class CatalogPersistenceErrorCodes
{
    public const string UnsupportedVersion = "catalog.json.unsupported_version";
    public const string Malformed = "catalog.json.malformed";
    public const string MapFailed = "catalog.json.map_failed";
    public const string IoFailed = "catalog.json.io_failed";
    public const string ByteOrderMarkPresent = "catalog.json.byte_order_mark_present";
}
