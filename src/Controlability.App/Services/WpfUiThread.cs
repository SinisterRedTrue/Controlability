using System.Windows.Threading;

namespace Controlability.App.Services;

public sealed class WpfUiThread(Dispatcher dispatcher) : IUiThread
{
    public void Post(Action action) => dispatcher.BeginInvoke(action);

    public void Post(Action action, TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            Post(action);
            return;
        }

        // The timer must be created on the UI thread to tick there.
        dispatcher.BeginInvoke(() =>
        {
            var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = delay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                action();
            };
            timer.Start();
        });
    }

    public IDisposable Every(TimeSpan interval, Action action)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = interval };
        timer.Tick += (_, _) => action();
        timer.Start();
        return new TimerHandle(timer);
    }

    private sealed class TimerHandle(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
