using Localizer.Application.Export;

namespace Localizer.Infrastructure.Export;

public sealed class DraftPreviewJsonWriter : IDraftPreviewWriter
{
    public Task<IReadOnlyList<string>> WriteAsync(
        string outputDirectory,
        IReadOnlyList<LocaleTexts> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(files);

        return LocaleJsonFileWriter.WriteFilesAsync(
            outputDirectory,
            files.Select(file => (file.Locale, (IEnumerable<KeyValuePair<string, string>>)file.Texts)),
            cancellationToken);
    }
}
