using Localizer.Application.Drafting;

namespace Localizer.Desktop.Tests.Fakes;

internal sealed class InMemoryDraftModelPreference : IDraftModelPreference
{
    public string? ModelName { get; set; }

    public Task<string?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(ModelName);

    public Task SaveAsync(string? modelName, CancellationToken cancellationToken = default)
    {
        ModelName = modelName;
        return Task.CompletedTask;
    }
}
