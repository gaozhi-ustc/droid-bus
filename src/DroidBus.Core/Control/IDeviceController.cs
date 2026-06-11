namespace DroidBus.Core.Control;

public interface IDeviceController
{
    Task TapAsync(string serial, int x, int y, CancellationToken ct = default);
    Task SwipeAsync(string serial, int x1, int y1, int x2, int y2, int durationMs, CancellationToken ct = default);
    Task KeyEventAsync(string serial, int keycode, CancellationToken ct = default);
    Task TextAsync(string serial, string text, CancellationToken ct = default);
    Task LaunchAppAsync(string serial, string pkg, CancellationToken ct = default);
    /// 执行原始 shell 命令(去掉前缀 "adb shell"/"adb" 后的部分)。
    Task ExecAsync(string serial, string shellCommand, CancellationToken ct = default);
    Task TypeUnicodeAsync(string serial, string text, CancellationToken ct = default);
}
