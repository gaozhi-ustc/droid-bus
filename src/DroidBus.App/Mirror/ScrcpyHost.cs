using System.Diagnostics;
using DroidBus.App.Interop;
using DroidBus.Core;
using DroidBus.Core.Mirror;
using DroidBus.Core.Models;

namespace DroidBus.App.Mirror;

/// 管理一台设备的 scrcpy 进程与窗口嵌入生命周期(平台无关,依赖 INativeWindowEmbedder)。
public sealed class ScrcpyHost : IDisposable
{
    private readonly BinaryLocator _bin;
    private readonly INativeWindowEmbedder _embedder;
    private readonly int _devW;
    private readonly int _devH;
    private Process? _proc;
    private IntPtr _child = IntPtr.Zero;
    private IntPtr _hostHandle = IntPtr.Zero;

    public ScrcpyHost(BinaryLocator bin, INativeWindowEmbedder embedder, int devW, int devH)
    {
        _bin = bin;
        _embedder = embedder;
        _devW = devW;
        _devH = devH;
    }

    public string? Serial { get; private set; }
    public event Action<int>? Crashed;

    /// 将 Avalonia NativeControlHost 的句柄提供给本实例。
    /// 若 scrcpy 窗口已就绪,立即嵌入;否则等窗口出现时嵌入。
    public void SetHostHandle(IntPtr handle)
    {
        _hostHandle = handle;
        if (_child != IntPtr.Zero && _hostHandle != IntPtr.Zero)
            _embedder.Embed(_child, _hostHandle);
    }

    /// 启动 scrcpy 进程,轮询定位其窗口(最多 15 秒),然后嵌入到已设置的 host 句柄。
    public async Task StartAsync(Device device, MirrorOptions options)
    {
        Serial = device.Serial;
        var title = ScrcpyArgsBuilder.WindowTitle(device.Serial);
        var args = ScrcpyArgsBuilder.Build(device.Serial, options);

        var psi = new ProcessStartInfo(_bin.Scrcpy)
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = _bin.Dir,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _proc.Exited += (_, _) =>
        {
            var code = _proc?.ExitCode ?? -1;
            if (code != 0) Crashed?.Invoke(code);
        };
        _proc.Start();

        // 轮询等待 scrcpy 渲染窗口出现(最多 15 秒)
        for (var i = 0; i < 150 && _child == IntPtr.Zero; i++)
        {
            await Task.Delay(100);
            _proc.Refresh();
            if (_proc.HasExited) { Crashed?.Invoke(_proc.ExitCode); return; }
            _child = _embedder.FindWindow(_proc.Id, title);
        }
        if (_child == IntPtr.Zero)
            throw new InvalidOperationException($"未能定位 {device.Serial} 的 scrcpy 窗口");

        if (_hostHandle != IntPtr.Zero)
            _embedder.Embed(_child, _hostHandle);
    }

    /// 按容器尺寸 + 设备宽高比重新缩放嵌入窗口。
    public void Resize(int containerW, int containerH)
    {
        if (_child == IntPtr.Zero) return;
        var box = Core.Control.Letterbox.Fit(containerW, containerH, _devW, _devH);
        if (box.Width <= 0 || box.Height <= 0) return;
        _embedder.MoveResize(_child, box.OffsetX, box.OffsetY, box.Width, box.Height);
    }

    public void Stop()
    {
        if (_child != IntPtr.Zero) { _embedder.Release(_child); _child = IntPtr.Zero; }
        try { if (_proc is { HasExited: false }) _proc.Kill(true); } catch { }
        _proc?.Dispose();
        _proc = null;
    }

    public void Dispose() => Stop();
}
