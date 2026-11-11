using Localizer.Desktop.ViewModels;

namespace Localizer.Desktop.Tests;

public sealed class ProjectFolderViewModelTests
{
    [Test]
    public void SoleCsv_BecomesTheImportFileAndMirrorsExport()
    {
        var model = new ProjectFolderViewModel("folder", ["UI.csv"], null, null);

        Assert.Multiple(() =>
        {
            Assert.That(model.ImportFileName, Is.EqualTo("UI.csv"));
            Assert.That(model.ExportFileName, Is.EqualTo("UI.csv"));
            Assert.That(model.MirrorExport, Is.True);
            Assert.That(model.CanSave, Is.True);
            Assert.That(model.ScanSummaryText, Is.EqualTo("Found 1 CSV file in this folder."));
        });
    }

    [Test]
    public void SeveralCsvFiles_LeaveImportEmptyUntilChosen()
    {
        var model = new ProjectFolderViewModel("folder", ["A.csv", "B.csv"], null, null);

        Assert.Multiple(() =>
        {
            Assert.That(model.ImportFileName, Is.Empty);
            Assert.That(model.CanSave, Is.False);
            Assert.That(model.ScanSummaryText, Is.EqualTo("Found 2 CSV files in this folder."));
        });
    }

    [Test]
    public void NoCsvFiles_AsksForTypedName()
    {
        var model = new ProjectFolderViewModel("folder", [], null, null);

        Assert.That(model.ScanSummaryText, Does.StartWith("No CSV files found"));
        Assert.That(model.CanSave, Is.False);
    }

    [Test]
    public void ExistingBinding_KeepsItsImportNameOverTheSoleCsv()
    {
        var model = new ProjectFolderViewModel("folder", ["Other.csv"], "UI.csv", "UI.csv");

        Assert.That(model.ImportFileName, Is.EqualTo("UI.csv"));
    }

    [Test]
    public void DifferentExportName_DisablesMirroring()
    {
        var model = new ProjectFolderViewModel("folder", ["UI.csv"], "UI.csv", "Out.csv");

        Assert.Multiple(() =>
        {
            Assert.That(model.MirrorExport, Is.False);
            Assert.That(model.ExportFileName, Is.EqualTo("Out.csv"));
        });
    }

    [Test]
    public void SameExportNameIgnoringCase_Mirrors()
    {
        var model = new ProjectFolderViewModel("folder", ["UI.csv"], "UI.csv", "ui.CSV");

        Assert.That(model.MirrorExport, Is.True);
        Assert.That(model.ExportFileName, Is.EqualTo("UI.csv"));
    }

    [Test]
    public void EditingImport_WhileMirroring_UpdatesExport()
    {
        var model = new ProjectFolderViewModel("folder", ["UI.csv"], null, null);

        model.ImportFileName = "Menu.csv";

        Assert.That(model.ExportFileName, Is.EqualTo("Menu.csv"));
    }

    [Test]
    public void EditingImport_WithoutMirroring_LeavesExportAlone()
    {
        var model = new ProjectFolderViewModel("folder", ["UI.csv"], "UI.csv", "Out.csv");

        model.ImportFileName = "Menu.csv";

        Assert.That(model.ExportFileName, Is.EqualTo("Out.csv"));
    }

    [Test]
    public void TurningMirrorOn_CopiesTheImportName()
    {
        var model = new ProjectFolderViewModel("folder", ["UI.csv"], "UI.csv", "Out.csv");

        model.MirrorExport = true;

        Assert.That(model.ExportFileName, Is.EqualTo("UI.csv"));
    }

    [Test]
    public void ClearingEitherName_DisablesSave()
    {
        var model = new ProjectFolderViewModel("folder", ["UI.csv"], "UI.csv", "Out.csv");
        Assert.That(model.CanSave, Is.True);

        model.ExportFileName = " ";
        Assert.That(model.CanSave, Is.False);

        model.ExportFileName = "Out.csv";
        model.ImportFileName = string.Empty;
        Assert.That(model.CanSave, Is.False);
    }
}
