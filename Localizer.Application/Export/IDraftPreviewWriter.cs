namespace Localizer.Application.Export;

public interface IDraftPreviewWriter
{
    Task<IReadOnlyList<string>> WriteAsync(
        string outputDirectory,
        IReadOnlyList<LocaleTexts> files,
        CancellationToken cancellationToken = default);
}
