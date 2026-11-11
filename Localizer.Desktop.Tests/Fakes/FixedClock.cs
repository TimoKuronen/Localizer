using Localizer.Application.Time;

namespace Localizer.Desktop.Tests.Fakes;

internal sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; } = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
}
