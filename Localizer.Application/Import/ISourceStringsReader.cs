namespace Localizer.Application.Import;

public interface ISourceStringsReader
{
    Task<SourceStringsDocument> ReadAsync(
        string path,
        CancellationToken cancellationToken = default);
}
