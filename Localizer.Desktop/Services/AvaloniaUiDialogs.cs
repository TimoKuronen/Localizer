using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Localizer.Application.UseCases;
using Localizer.Core.Syntax;
using Localizer.Desktop.ViewModels;
using Localizer.Desktop.Views;

namespace Localizer.Desktop.Services;

public sealed class AvaloniaUiDialogs : IUiDialogs
{
    private Window? _owner;

    public void Attach(Window owner) =>
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public async Task<string?> PickOpenCatalogPathAsync(CancellationToken cancellationToken = default)
    {
        var owner = RequireOwner();
        cancellationToken.ThrowIfCancellationRequested();

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open catalog",
            AllowMultiple = false,
            FileTypeFilter = [CatalogFileType],
            SuggestedStartLocation = await GetStorageFolderAsync(owner).ConfigureAwait(true)
        }).ConfigureAwait(true);

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<string?> PickSaveCatalogPathAsync(
        string? suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        var owner = RequireOwner();
        cancellationToken.ThrowIfCancellationRequested();

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save catalog",
            SuggestedFileName = suggestedFileName ?? "catalog.json",
            DefaultExtension = "json",
            FileTypeChoices = [CatalogFileType],
            SuggestedStartLocation = await GetStorageFolderAsync(owner).ConfigureAwait(true)
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    public async Task<CreateCatalogRequest?> PromptCreateCatalogAsync()
    {
        var owner = RequireOwner();
        var viewModel = new NewCatalogViewModel();
        var window = new NewCatalogWindow
        {
            DataContext = viewModel
        };

        var accepted = await window.ShowDialog<bool>(owner).ConfigureAwait(true);
        if (!accepted)
        {
            return null;
        }

        var required = viewModel.RequiredLocalesText
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return new CreateCatalogRequest
        {
            CatalogId = viewModel.CatalogId.Trim(),
            SourceLocale = viewModel.SourceLocale.Trim(),
            RequiredLocales = required,
            DefaultSyntaxProfile = viewModel.UseCompositeSyntax
                ? MessageSyntaxProfile.Composite
                : MessageSyntaxProfile.Plain
        };
    }

    public async Task<AddCatalogEntryRequest?> PromptAddEntryAsync()
    {
        var owner = RequireOwner();
        var viewModel = new AddEntryViewModel();
        var window = new AddEntryWindow
        {
            DataContext = viewModel
        };

        var accepted = await window.ShowDialog<bool>(owner).ConfigureAwait(true);
        if (!accepted)
        {
            return null;
        }

        return new AddCatalogEntryRequest
        {
            Key = viewModel.Key.Trim(),
            SourceText = viewModel.SourceText,
            DeveloperNotes = string.IsNullOrWhiteSpace(viewModel.DeveloperNotes)
                ? null
                : viewModel.DeveloperNotes.Trim()
        };
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var owner = RequireOwner();
        var window = new MessageWindow
        {
            Title = title,
            DataContext = new MessageViewModel { Message = message }
        };

        await window.ShowDialog(owner).ConfigureAwait(true);
    }

    private Window RequireOwner() =>
        _owner ?? throw new InvalidOperationException("UI dialogs are not attached to a window.");

    private static async Task<IStorageFolder?> GetStorageFolderAsync(Window owner)
    {
        var path = WorkspaceStorage.EnsureDirectory();
        return await owner.StorageProvider.TryGetFolderFromPathAsync(path).ConfigureAwait(true);
    }

    private static FilePickerFileType CatalogFileType { get; } = new("Localizer catalog")
    {
        Patterns = ["*.json"]
    };
}
