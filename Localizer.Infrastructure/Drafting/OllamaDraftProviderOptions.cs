namespace Localizer.Infrastructure.Drafting;

public sealed class OllamaDraftProviderOptions
{
    public Uri BaseAddress { get; init; } = new("http://127.0.0.1:11434/");

    public string ModelName { get; init; } = "llama3.2";

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
