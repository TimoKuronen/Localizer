namespace Localizer.Infrastructure.Drafting;

public sealed class OllamaDraftProviderOptions
{
    public Uri BaseAddress { get; init; } = new("http://127.0.0.1:11434/");

    /// <summary>
    /// Model to draft with. When null, the provider uses the sole chat-capable installed model.
    /// </summary>
    public string? ModelName { get; init; }

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
