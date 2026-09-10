using Avalonia.Controls;
using Localizer.Application.UseCases;

namespace Localizer.Desktop.Services;

public interface IUiDialogs
{
    void Attach(Window owner);

    Task<string?> PickOpenCatalogPathAsync(CancellationToken cancellationToken = default);

    Task<string?> PickSaveCatalogPathAsync(
        string? suggestedFileName,
        CancellationToken cancellationToken = default);

    Task<CreateCatalogRequest?> PromptCreateCatalogAsync();

    Task<AddCatalogEntryRequest?> PromptAddEntryAsync();

    Task ShowMessageAsync(string title, string message);
}
