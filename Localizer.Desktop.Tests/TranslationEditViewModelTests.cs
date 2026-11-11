using Localizer.Core.Lifecycle;
using Localizer.Desktop.ViewModels;

namespace Localizer.Desktop.Tests;

public sealed class TranslationEditViewModelTests
{
    [Test]
    public void Draft_WithText_CanApprove()
    {
        var row = new TranslationEditViewModel("es", "Hola", TranslationEffectiveStatus.Draft);

        Assert.That(row.CanApprove, Is.True);
        Assert.That(row.ApproveHint, Does.StartWith("Approve this text"));
    }

    [Test]
    public void Draft_WithEmptyText_CannotApprove()
    {
        var row = new TranslationEditViewModel("es", string.Empty, TranslationEffectiveStatus.Draft);

        Assert.That(row.CanApprove, Is.False);
        Assert.That(row.ApproveHint, Is.EqualTo("Enter a translation before approving."));
    }

    [Test]
    public void Approved_Unedited_CannotApproveAgain()
    {
        var row = new TranslationEditViewModel("es", "Hola", TranslationEffectiveStatus.Approved);

        Assert.That(row.CanApprove, Is.False);
        Assert.That(row.ApproveHint, Is.EqualTo("Already approved."));
    }

    [Test]
    public void Approved_Edited_CanApprove()
    {
        var row = new TranslationEditViewModel("es", "Hola", TranslationEffectiveStatus.Approved);

        row.Text = "Hola!";

        Assert.That(row.CanApprove, Is.True);
        Assert.That(row.HasTextChanged, Is.True);
    }

    [Test]
    public void Approved_EditedBackToOriginal_CannotApprove()
    {
        var row = new TranslationEditViewModel("es", "Hola", TranslationEffectiveStatus.Approved);

        row.Text = "Hola!";
        row.Text = "Hola";

        Assert.That(row.CanApprove, Is.False);
    }

    [Test]
    public void Missing_NeedsTypedText()
    {
        var row = new TranslationEditViewModel("es", string.Empty, TranslationEffectiveStatus.Missing);
        Assert.That(row.CanApprove, Is.False);

        row.Text = "Hola";

        Assert.That(row.CanApprove, Is.True);
    }

    [Test]
    public void Stale_Unedited_ExplainsSourceChange()
    {
        var row = new TranslationEditViewModel("es", "Hola", TranslationEffectiveStatus.Stale);

        Assert.That(row.CanApprove, Is.False);
        Assert.That(row.ApproveHint, Does.StartWith("Source changed"));
        Assert.That(row.IsStale, Is.True);
    }

    [Test]
    public void Stale_Edited_CanApprove()
    {
        var row = new TranslationEditViewModel("es", "Hola", TranslationEffectiveStatus.Stale);

        row.Text = "Hola otra vez";

        Assert.That(row.CanApprove, Is.True);
    }

    [TestCase(TranslationEffectiveStatus.Draft, true)]
    [TestCase(TranslationEffectiveStatus.Approved, true)]
    [TestCase(TranslationEffectiveStatus.Stale, false)]
    [TestCase(TranslationEffectiveStatus.Missing, false)]
    public void CanInvalidate_OnlyForDraftAndApproved(TranslationEffectiveStatus status, bool expected)
    {
        var row = new TranslationEditViewModel("es", "Hola", status);

        Assert.That(row.CanInvalidate, Is.EqualTo(expected));
    }

    [Test]
    public void Refresh_ResetsOriginalTextAndStatus()
    {
        var row = new TranslationEditViewModel("es", "Hola", TranslationEffectiveStatus.Draft);
        row.Text = "Hola!";

        row.Refresh("Hola!", TranslationEffectiveStatus.Approved);

        Assert.Multiple(() =>
        {
            Assert.That(row.HasTextChanged, Is.False);
            Assert.That(row.IsApproved, Is.True);
            Assert.That(row.Status, Is.EqualTo("Approved"));
            Assert.That(row.CanApprove, Is.False);
            Assert.That(row.CanInvalidate, Is.True);
        });
    }

    [Test]
    public void TextEdit_NotifiesTheOwner()
    {
        var edits = 0;
        var row = new TranslationEditViewModel(
            "es",
            "Hola",
            TranslationEffectiveStatus.Draft,
            textEdited: () => edits++);

        row.Text = "Hola!";

        Assert.That(edits, Is.EqualTo(1));
    }
}
