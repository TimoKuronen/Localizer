namespace Localizer.Application.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
