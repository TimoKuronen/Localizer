using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;
using Localizer.Core.Lifecycle;

namespace Localizer.Application.Tests;

public sealed class MatchingTranslationsUseCaseTests
{
    private readonly FindMatchingTranslationsUseCase _find = new();
    private readonly ApproveMatchingTranslationsUseCase _approve = new();

    [Test]
    public void Find_ListsRepeatsThatNeedApprovalAndFlagsDifferences()
    {
        var catalog = CreateCatalog();
        Add(catalog, "a", "Open");
        Add(catalog, "b", "Open");
        Add(catalog, "c", "Open", notes: "Adjective on a shop sign.");
        Add(catalog, "d", "Open");
        Add(catalog, "e", "open");
        Approve(catalog, "a", "Abrir");
        Approve(catalog, "d", "Abre");

        var matches = _find.Execute(catalog, "a", "es");

        Assert.That(
            matches.Select(match => (match.Key, match.IsIdentical, match.Status)),
            Is.EqualTo(new[]
            {
                ("b", true, TranslationEffectiveStatus.Missing),
                ("c", false, TranslationEffectiveStatus.Missing)
            }));
    }

    [Test]
    public void Approve_CopiesTextToTargetsAndKeepsOtherApprovedText()
    {
        var catalog = CreateCatalog();
        Add(catalog, "a", "Open");
        Add(catalog, "b", "Open");
        Add(catalog, "c", "Open");
        Approve(catalog, "a", "Abrir");
        Approve(catalog, "c", "Abre");

        var result = Execute(catalog, "b", "c");

        Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.ApprovedKeys, Is.EqualTo(new[] { "b" }));
            Assert.That(result.Value.BlockedMessages.Single(), Does.StartWith("c:"));
            Assert.That(Status(catalog, "b"), Is.EqualTo(TranslationEffectiveStatus.Approved));
            Assert.That(Text(catalog, "b"), Is.EqualTo("Abrir"));
            Assert.That(Text(catalog, "c"), Is.EqualTo("Abre"));
        });
    }

    [Test]
    public void Approve_LeavesATargetThatFailsValidationUnchanged()
    {
        var catalog = CreateCatalog();
        Add(catalog, "a", "Open");
        Add(catalog, "b", "Open", maxGraphemes: 2);
        Approve(catalog, "a", "Abrir");

        var result = Execute(catalog, "b");

        Assert.That(result.Value!.ApprovedKeys, Is.Empty);
        Assert.That(result.Value.BlockedMessages, Has.Count.EqualTo(1));
        Assert.That(Status(catalog, "b"), Is.EqualTo(TranslationEffectiveStatus.Missing));
    }

    [Test]
    public void Approve_RequiresAnApprovedSource()
    {
        var catalog = CreateCatalog();
        Add(catalog, "a", "Open");
        Add(catalog, "b", "Open");

        var result = Execute(catalog, "b");

        Assert.That(result.ErrorCode, Is.EqualTo(UseCaseErrorCodes.TranslationNotApproved));
    }

    private UseCaseResult<ApproveMatchingTranslationsOutcome> Execute(Catalog catalog, params string[] targets) =>
        _approve.Execute(catalog, new ApproveMatchingTranslationsRequest
        {
            SourceKey = "a",
            Locale = "es",
            TargetKeys = targets
        });

    private static TranslationEffectiveStatus Status(Catalog catalog, string key) =>
        catalog.GetEffectiveStatus(catalog.Entries[EntryKey.Create(key)], Locale.Create("es"));

    private static string Text(Catalog catalog, string key) =>
        catalog.Entries[EntryKey.Create(key)].Translations[Locale.Create("es")].Text;

    private static Catalog CreateCatalog() =>
        new CreateCatalogUseCase().Execute(new CreateCatalogRequest
        {
            CatalogId = "catalog",
            SourceLocale = "en",
            RequiredLocales = ["es"]
        }).Value!;

    private static void Add(Catalog catalog, string key, string source, string? notes = null, int? maxGraphemes = null)
    {
        var added = new AddCatalogEntryUseCase().Execute(
            catalog,
            new AddCatalogEntryRequest { Key = key, SourceText = source, DeveloperNotes = notes });
        Assert.That(added.Succeeded, Is.True, added.ErrorMessage);
        if (maxGraphemes is not null)
        {
            var entry = catalog.Entries[EntryKey.Create(key)];
            catalog.ReplaceEntry(entry with { Constraints = new EntryConstraints { MaxGraphemes = maxGraphemes } });
        }
    }

    private static void Approve(Catalog catalog, string key, string text)
    {
        var drafted = new SetTranslationDraftUseCase().Execute(
            catalog,
            new SetTranslationDraftRequest { Key = key, Locale = "es", Text = text });
        Assert.That(drafted.Succeeded, Is.True, drafted.ErrorMessage);
        var approved = new ApproveTranslationUseCase().Execute(
            catalog,
            new ApproveTranslationRequest { Key = key, Locale = "es" });
        Assert.That(approved.Succeeded, Is.True, approved.ErrorMessage);
    }
}
