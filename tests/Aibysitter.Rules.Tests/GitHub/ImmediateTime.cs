namespace Aibysitter.Rules.Tests.GitHub;

/// <summary>Timers fire at once; each requested delay is recorded.</summary>
internal sealed class ImmediateTime : TimeProvider
{
    public List<TimeSpan> Delays { get; } = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (Delays)
        {
            Delays.Add(dueTime);
        }

        var timer = new Timer(callback, state, Timeout.Infinite, Timeout.Infinite);
        timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        return timer;
    }
}
