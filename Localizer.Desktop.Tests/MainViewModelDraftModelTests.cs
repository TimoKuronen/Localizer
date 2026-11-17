using Avalonia.Headless.NUnit;
using Localizer.Desktop.Services;
using Localizer.Desktop.Tests.Fakes;

namespace Localizer.Desktop.Tests;

public sealed class MainViewModelDraftModelTests
{
    [AvaloniaTest]
    public void ChooseModel_OffersInstalledModelsAndSavesTheChoice()
    {
        var fixture = new MainViewModelFixture();
        fixture.ModelCatalog.Models = ["qwen3.5:4b", "mistral:latest"];
        fixture.ModelPreference.ModelName = "qwen3.5:4b";
        fixture.Dialogs.ModelResult = new DraftModelChoice("mistral:latest");

        fixture.ViewModel.ChooseDraftModelCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ModelPreference.ModelName == "mistral:latest");

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.PromptedModels, Is.EqualTo(new[] { "qwen3.5:4b", "mistral:latest" }));
            Assert.That(fixture.Dialogs.PromptedSavedModel, Is.EqualTo("qwen3.5:4b"));
            Assert.That(fixture.ViewModel.StatusText, Is.EqualTo("Draft model: mistral:latest."));
        });
    }

    [AvaloniaTest]
    public void ChooseModel_Automatic_ClearsTheSavedChoice()
    {
        var fixture = new MainViewModelFixture();
        fixture.ModelCatalog.Models = ["qwen3.5:4b"];
        fixture.ModelPreference.ModelName = "qwen3.5:4b";
        fixture.Dialogs.ModelResult = new DraftModelChoice(null);

        fixture.ViewModel.ChooseDraftModelCommand.Execute(null);
        fixture.PumpUntil(() => fixture.ModelPreference.ModelName is null);

        Assert.That(fixture.ViewModel.StatusText, Is.EqualTo("Draft model: automatic."));
    }

    [AvaloniaTest]
    public void ChooseModel_Cancelled_KeepsTheSavedChoice()
    {
        var fixture = new MainViewModelFixture();
        fixture.ModelCatalog.Models = ["qwen3.5:4b", "mistral:latest"];
        fixture.ModelPreference.ModelName = "qwen3.5:4b";
        fixture.Dialogs.ModelResult = null;

        fixture.ViewModel.ChooseDraftModelCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.ModelPromptCount == 1);

        Assert.That(fixture.ModelPreference.ModelName, Is.EqualTo("qwen3.5:4b"));
    }

    [AvaloniaTest]
    public void ChooseModel_FixedByEnvironment_ExplainsInsteadOfPrompting()
    {
        var fixture = new MainViewModelFixture(environmentModel: "mistral:latest");

        fixture.ViewModel.ChooseDraftModelCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.Messages.Count == 1);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.ModelPromptCount, Is.Zero);
            Assert.That(fixture.Dialogs.Messages.Single(), Does.Contain("LOCALIZER_OLLAMA_MODEL").And.Contain("mistral:latest"));
        });
    }

    [AvaloniaTest]
    public void ChooseModel_WhenTheServerCannotBeReached_ReportsTheReason()
    {
        var fixture = new MainViewModelFixture();
        fixture.ModelCatalog.FailureMessage = "Could not reach Ollama.";

        fixture.ViewModel.ChooseDraftModelCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.Messages.Count == 1);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.Messages.Single(), Does.Contain("Could not reach Ollama."));
            Assert.That(fixture.Dialogs.ModelPromptCount, Is.Zero);
        });
    }

    [AvaloniaTest]
    public void ChooseModel_WithNoInstalledModels_SaysSo()
    {
        var fixture = new MainViewModelFixture();

        fixture.ViewModel.ChooseDraftModelCommand.Execute(null);
        fixture.PumpUntil(() => fixture.Dialogs.Messages.Count == 1);

        Assert.That(fixture.Dialogs.Messages.Single(), Does.Contain("No chat-capable models"));
        Assert.That(fixture.Dialogs.ModelPromptCount, Is.Zero);
    }
}
