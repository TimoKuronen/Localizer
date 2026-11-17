using Localizer.Application.Drafting;

namespace Localizer.Desktop.Tests.Fakes;

internal sealed class FakeDraftModelCatalog(IReadOnlyList<string> models) : IDraftModelCatalog
{
    public IReadOnlyList<string> Models { get; set; } = models;

    public string? FailureMessage { get; set; }

    public Task<IReadOnlyList<string>> ListChatModelsAsync(CancellationToken cancellationToken = default) =>
        FailureMessage is null
            ? Task.FromResult(Models)
            : Task.FromException<IReadOnlyList<string>>(new InvalidOperationException(FailureMessage));
}
