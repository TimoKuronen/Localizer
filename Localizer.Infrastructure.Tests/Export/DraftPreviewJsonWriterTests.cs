using System.Text;
using Localizer.Application.Export;
using Localizer.Core.Identity;
using Localizer.Infrastructure.Export;

namespace Localizer.Infrastructure.Tests.Export;

public sealed class DraftPreviewJsonWriterTests
{
    [Test]
    public async Task WriteAsync_WritesCompactUtf8FilesInOrdinalKeyOrder()
    {
        var directory = TempDirectory.Create("localizer-preview-tests");

        var written = await new DraftPreviewJsonWriter().WriteAsync(directory,
        [
            new LocaleTexts
            {
                Locale = Locale.Create("fi"),
                Texts =
                [
                    new KeyValuePair<string, string>("z.last", "Viimeinen"),
                    new KeyValuePair<string, string>("a.first", "Ensimmäinen {0}")
                ]
            },
            new LocaleTexts { Locale = Locale.Create("es"), Texts = [] }
        ]);

        var fiBytes = await File.ReadAllBytesAsync(Path.Combine(directory, "fi.json"));
        Assert.Multiple(() =>
        {
            Assert.That(written, Is.EqualTo(new[] { Path.Combine(directory, "es.json"), Path.Combine(directory, "fi.json") }));
            Assert.That(fiBytes[0], Is.Not.EqualTo(0xEF));
            Assert.That(
                Encoding.UTF8.GetString(fiBytes),
                Is.EqualTo("{\"a.first\":\"Ensimm\\u00E4inen {0}\",\"z.last\":\"Viimeinen\"}\n"));
            Assert.That(File.ReadAllText(Path.Combine(directory, "es.json")), Is.EqualTo("{}\n"));
        });
    }
}
