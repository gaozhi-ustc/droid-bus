using DroidBus.Core.Control;
using DroidBus.Core.Script;
using DroidBus.Core.Time;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ScriptRunnerTests
{
    private sealed class FakeController : IDeviceController
    {
        public List<string> Log { get; } = new();
        public Task TapAsync(string s, int x, int y, CancellationToken ct = default) { Log.Add($"tap {x} {y}"); return Task.CompletedTask; }
        public Task SwipeAsync(string s, int x1, int y1, int x2, int y2, int ms, CancellationToken ct = default) { Log.Add($"swipe {x1} {y1} {x2} {y2} {ms}"); return Task.CompletedTask; }
        public Task KeyEventAsync(string s, int k, CancellationToken ct = default) { Log.Add($"key {k}"); return Task.CompletedTask; }
        public Task TextAsync(string s, string t, CancellationToken ct = default) { Log.Add($"text {t}"); return Task.CompletedTask; }
        public Task LaunchAppAsync(string s, string p, CancellationToken ct = default) { Log.Add($"launch {p}"); return Task.CompletedTask; }
        public Task ExecAsync(string s, string c, CancellationToken ct = default) { Log.Add($"exec {c}"); return Task.CompletedTask; }
        public List<(string, string)> Unicode { get; } = new();
        public Task TypeUnicodeAsync(string s, string t, CancellationToken ct = default) { Unicode.Add((s, t)); return Task.CompletedTask; }
    }

    private sealed class FakeClock : IClock
    {
        public List<TimeSpan> Delays { get; } = new();
        public Task DelayAsync(TimeSpan d, CancellationToken ct = default) { Delays.Add(d); return Task.CompletedTask; }
        public int RandomMilliseconds(int min, int max) => min;
    }

    [Fact]
    public async Task Translates_commands_to_controller_calls()
    {
        var ctrl = new FakeController();
        var clock = new FakeClock();
        var runner = new ScriptRunner(ctrl, clock);

        var cmds = new ScriptCommand[]
        {
            new TapCommand(10, 20),
            new SwipeCommand(1, 2, 3, 4),
            new HomeCommand(),
            new BackCommand(),
            new DelayCommand(TimeSpan.FromSeconds(2)),
            new RandomDelayCommand(),
            new LaunchAppCommand("com.x"),
            new ExecCommand("input keyevent 24"),
            new AdbTextCommand("hi"),
        };

        await runner.RunAsync("S1", cmds);

        ctrl.Log.Should().Equal(
            "tap 10 20",
            "swipe 1 2 3 4 200",   // 普通滑动固定时长 200ms(见实现 SwipeMs)
            "key 3",
            "key 4",
            "launch com.x",
            "exec input keyevent 24",
            "text hi");
        clock.Delays[0].Should().Be(TimeSpan.FromSeconds(2));
        clock.Delays.Should().HaveCount(2); // 显式 2s + 一次随机延时
    }

    [Fact]
    public async Task InputText_uses_ime_broadcast()
    {
        var ctrl = new FakeController();
        var runner = new ScriptRunner(ctrl, new FakeClock());
        await runner.RunAsync("S1", new ScriptCommand[] { new InputTextCommand("你好") }, default);
        ctrl.Unicode.Should().ContainSingle().Which.Should().Be(("S1", "你好"));
    }
}
