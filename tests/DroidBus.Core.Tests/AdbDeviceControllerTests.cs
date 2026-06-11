using DroidBus.Core.Control;
using DroidBus.Core.Process;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class AdbDeviceControllerTests
{
    private sealed class RecordingRunner : IProcessRunner
    {
        public List<string[]> Calls { get; } = new();
        public Task<ProcessResult> RunAsync(string exe, IReadOnlyList<string> args, CancellationToken ct = default)
        {
            Calls.Add(args.ToArray());
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    [Fact]
    public async Task Tap_invokes_adb_input_tap()
    {
        var runner = new RecordingRunner();
        var c = new AdbDeviceController(runner, "adb.exe");
        await c.TapAsync("S1", 10, 20);
        runner.Calls.Single().Should().Equal("-s", "S1", "shell", "input", "tap", "10", "20");
    }

    [Fact]
    public async Task Exec_runs_raw_shell()
    {
        var runner = new RecordingRunner();
        var c = new AdbDeviceController(runner, "adb.exe");
        await c.ExecAsync("S1", "input keyevent 24");
        runner.Calls.Single().Should().Equal("-s", "S1", "shell", "input", "keyevent", "24");
    }

    [Fact]
    public async Task Exec_keeps_quoted_argument_as_one_token()
    {
        var runner = new RecordingRunner();
        var c = new AdbDeviceController(runner, "adb.exe");
        await c.ExecAsync("S1", "am start -n com.foo/.Bar -e key \"hello world\"");
        runner.Calls.Single().Should().Equal(
            "-s", "S1", "shell", "am", "start", "-n", "com.foo/.Bar", "-e", "key", "hello world");
    }
}
