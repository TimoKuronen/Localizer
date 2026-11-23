using CommunityToolkit.Mvvm.Input;
using Localizer.Application.UseCases;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task ConfigureCatalogAsync()
    {
        if (_session.Catalog is not { } catalog
            || !CanRunCatalogCommands
            || !await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var current = new ConfigureCatalogSettingsRequest
        {
            SourceLocale = catalog.SourceLocale.Value,
            RequiredLocales = catalog.RequiredLocales.Select(locale => locale.Value).ToList(),
            DefaultSyntaxProfile = catalog.DefaultSyntaxProfile,
            ProjectContext = catalog.ProjectContext,
            Glossary = catalog.Glossary
        };

        var request = await _dialogs.PromptCatalogSettingsAsync(current).ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        if (IsUnchanged(current, request))
        {
            StatusText = "Catalog settings unchanged.";
            return;
        }

        var result = _session.ConfigureSettings(request);
        if (!result.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Catalog settings failed", UseCaseErrorText.Format(result)).ConfigureAwait(true);
            return;
        }

        IsDirty = true;
        RefreshFromCatalog(selectKey: SelectedEntry?.Key);
        RunValidation();
        StatusText = "Updated catalog settings.";
    }

    [RelayCommand]
    private async Task ChooseDraftModelAsync(CancellationToken cancellationToken)
    {
        if (_draftModels.IsFixedByEnvironment)
        {
            await _dialogs.ShowMessageAsync(
                "Draft model",
                $"The draft model is fixed to '{_draftModels.EnvironmentModel}' by LOCALIZER_OLLAMA_MODEL. Unset the variable to choose a model here.").ConfigureAwait(true);
            return;
        }

        IReadOnlyList<string> models;
        try
        {
            models = await _draftModels.ListModelsAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _dialogs.ShowMessageAsync("Draft model", ex.Message).ConfigureAwait(true);
            return;
        }

        if (models.Count == 0)
        {
            await _dialogs.ShowMessageAsync(
                "Draft model",
                "No chat-capable models are installed on the Ollama server.").ConfigureAwait(true);
            return;
        }

        var saved = await _draftModels.GetSavedAsync(cancellationToken).ConfigureAwait(true);
        var choice = await _dialogs.PromptDraftModelAsync(models, saved).ConfigureAwait(true);
        if (choice is null)
        {
            return;
        }

        await _draftModels.SaveAsync(choice.ModelName, cancellationToken).ConfigureAwait(true);
        StatusText = choice.ModelName is null
            ? "Draft model: automatic."
            : $"Draft model: {choice.ModelName}.";
    }

    private static string? NormalizeGuidance(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUnchanged(ConfigureCatalogSettingsRequest current, ConfigureCatalogSettingsRequest request) =>
        string.Equals(current.SourceLocale, request.SourceLocale, StringComparison.OrdinalIgnoreCase)
        && current.DefaultSyntaxProfile == request.DefaultSyntaxProfile
        && NormalizeGuidance(current.ProjectContext) == NormalizeGuidance(request.ProjectContext)
        && NormalizeGuidance(current.Glossary) == NormalizeGuidance(request.Glossary)
        && current.RequiredLocales.Order(StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(request.RequiredLocales.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
}
