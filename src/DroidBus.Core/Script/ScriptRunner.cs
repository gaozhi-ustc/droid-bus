using DroidBus.Core.Control;
using DroidBus.Core.Time;

namespace DroidBus.Core.Script;

public sealed class ScriptRunner
{
    private const int Home = 3, Back = 4;
    private const int SwipeMs = 200, LongPressMs = 800;
    private const int RandomMin = 300, RandomMax = 1200;

    private readonly IDeviceController _ctrl;
    private readonly IClock _clock;

    public ScriptRunner(IDeviceController ctrl, IClock clock)
    {
        _ctrl = ctrl;
        _clock = clock;
    }

    public async Task RunAsync(string serial, IReadOnlyList<ScriptCommand> commands, CancellationToken ct = default)
    {
        foreach (var cmd in commands)
        {
            ct.ThrowIfCancellationRequested();
            switch (cmd)
            {
                case TapCommand t: await _ctrl.TapAsync(serial, t.X, t.Y, ct); break;
                case FastTapCommand t: await _ctrl.TapAsync(serial, t.X, t.Y, ct); break;
                case FastDoubleTapCommand t:
                    await _ctrl.TapAsync(serial, t.X, t.Y, ct);
                    await _ctrl.TapAsync(serial, t.X, t.Y, ct);
                    break;
                case LongPressCommand l: await _ctrl.SwipeAsync(serial, l.X, l.Y, l.X, l.Y, LongPressMs, ct); break;
                case SwipeCommand s: await _ctrl.SwipeAsync(serial, s.X1, s.Y1, s.X2, s.Y2, SwipeMs, ct); break;
                case HomeCommand: await _ctrl.KeyEventAsync(serial, Home, ct); break;
                case BackCommand: await _ctrl.KeyEventAsync(serial, Back, ct); break;
                case DelayCommand d: await _clock.DelayAsync(d.Duration, ct); break;
                case RandomDelayCommand:
                    await _clock.DelayAsync(TimeSpan.FromMilliseconds(_clock.RandomMilliseconds(RandomMin, RandomMax)), ct);
                    break;
                case LaunchAppCommand a: await _ctrl.LaunchAppAsync(serial, a.Package, ct); break;
                case ExecCommand e: await _ctrl.ExecAsync(serial, e.ShellCommand, ct); break;
                case AdbTextCommand x: await _ctrl.TextAsync(serial, x.Text, ct); break;
                case InputTextCommand x: await _ctrl.TypeUnicodeAsync(serial, x.Text, ct); break;
            }
        }
    }
}
