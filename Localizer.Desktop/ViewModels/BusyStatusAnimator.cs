using Avalonia.Threading;

namespace Localizer.Desktop.ViewModels;

/// <summary>
/// Animates a trailing dot suffix on a status line while an operation runs and reports each
/// rendered text through a callback.
/// </summary>
public sealed class BusyStatusAnimator
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(450);

    private readonly Action<string> _publish;
    private DispatcherTimer? _timer;
    private int _tick;
    private string _baseStatus = string.Empty;

    public BusyStatusAnimator(Action<string> publish)
    {
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
    }

    public void SetBusy(bool isBusy)
    {
        if (isBusy)
        {
            _tick = 0;
            _timer ??= new DispatcherTimer { Interval = TickInterval };
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            _timer.Start();
            Publish();
            return;
        }

        _timer?.Stop();
        _baseStatus = string.Empty;
    }

    public void Begin(string baseStatus)
    {
        _baseStatus = baseStatus;
        _tick = 0;
        Publish();
    }

    private void OnTick(object? sender, EventArgs e) => Publish();

    private void Publish()
    {
        if (string.IsNullOrWhiteSpace(_baseStatus))
        {
            return;
        }

        var dots = (_tick % 3) switch
        {
            0 => ".",
            1 => "..",
            _ => "..."
        };
        _tick++;
        _publish($"{_baseStatus}{dots}");
    }
}
