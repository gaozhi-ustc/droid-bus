using DroidBus.Core.Adb;
using DroidBus.Core.Process;

namespace DroidBus.Core.Control;

public sealed class AdbDeviceController : IDeviceController
{
    private readonly IProcessRunner _runner;
    private readonly string _adb;

    public AdbDeviceController(IProcessRunner runner, string adbPath)
    {
        _runner = runner;
        _adb = adbPath;
    }

    public Task TapAsync(string s, int x, int y, CancellationToken ct = default)
        => _runner.RunAsync(_adb, AdbCommands.Tap(s, x, y), ct);

    public Task SwipeAsync(string s, int x1, int y1, int x2, int y2, int ms, CancellationToken ct = default)
        => _runner.RunAsync(_adb, AdbCommands.Swipe(s, x1, y1, x2, y2, ms), ct);

    public Task KeyEventAsync(string s, int keycode, CancellationToken ct = default)
        => _runner.RunAsync(_adb, AdbCommands.KeyEvent(s, keycode), ct);

    public Task TextAsync(string s, string text, CancellationToken ct = default)
        => _runner.RunAsync(_adb, AdbCommands.Text(s, text), ct);

    public Task LaunchAppAsync(string s, string pkg, CancellationToken ct = default)
        => _runner.RunAsync(_adb, AdbCommands.StartApp(s, pkg), ct);

    public Task ExecAsync(string s, string shellCommand, CancellationToken ct = default)
    {
        // 把 "input keyevent 24" 拆成 args 跟在 "-s S shell" 后面;
        // 同时容忍用户写了 "adb shell xxx" / "shell xxx" 前缀。
        var cmd = shellCommand.Trim();
        if (cmd.StartsWith("adb ", StringComparison.OrdinalIgnoreCase)) cmd = cmd[4..].Trim();
        if (cmd.StartsWith("shell ", StringComparison.OrdinalIgnoreCase)) cmd = cmd[6..].Trim();
        var args = new List<string> { "-s", s, "shell" };
        args.AddRange(ShellArgs.Split(cmd));
        return _runner.RunAsync(_adb, args, ct);
    }

    public Task TypeUnicodeAsync(string s, string text, CancellationToken ct = default)
        => _runner.RunAsync(_adb, DroidBus.Core.Input.ImeCommands.TypeUnicode(s, text), ct);
}
