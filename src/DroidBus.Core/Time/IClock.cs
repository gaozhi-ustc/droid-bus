namespace DroidBus.Core.Time;

public interface IClock
{
    Task DelayAsync(TimeSpan duration, CancellationToken ct = default);
    int RandomMilliseconds(int minMs, int maxMs);
}

public sealed class SystemClock : IClock
{
    private readonly Random _rng = new();
    public Task DelayAsync(TimeSpan d, CancellationToken ct = default) => Task.Delay(d, ct);
    public int RandomMilliseconds(int minMs, int maxMs) => _rng.Next(minMs, maxMs);
}
