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
    private IntPtr _container = IntPtr.Zero;
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

    public void SetHostHandle(IntPtr handle)
    {
        _hostHandle = handle;
    }

    /// 启动 scrcpy 进程,轮询定位其窗口,然后嵌入到容器窗口。
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

        DebugLog.Write($"ScrcpyHost[{device.Serial}]: found child=0x{_child:X} hostHandle=0x{_hostHandle:X}");

        // 在 Avalonia 的顶层窗口内创建容器,再把 scrcpy reparent 进容器
        if (_hostHandle != IntPtr.Zero)
        {
            _container = _embedder.CreateEmbedContainer(_hostHandle, 0, 0, 1, 1);
            _embedder.Embed(_child, _container);

            // SDL 会在 reparent 后尝试恢复窗口尺寸,需多次强制 resize
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(50);
                _embedder.MoveResize(_child, 0, 0, 1, 1);
            }
        }
    }

    /// 把容器窗口放进顶层窗口内的目标矩形(areaX/Y/W/H,物理像素),
    /// 按设备宽高比 letterbox 居中。
    public void Resize(int areaX, int areaY, int areaW, int areaH)
    {
        if (_container == IntPtr.Zero) return;
        var box = Core.Control.Letterbox.Fit(areaW, areaH, _devW, _devH);
        if (box.Width <= 0 || box.Height <= 0) return;
        var x = areaX + box.OffsetX;
        var y = areaY + box.OffsetY;
        _embedder.MoveResizeContainer(_container, x, y, box.Width, box.Height);
        _embedder.MoveResize(_child, 0, 0, box.Width, box.Height);
    }

    public void Stop()
    {
        try { if (_proc is { HasExited: false }) _proc.Kill(true); } catch { }
        _proc?.Dispose();
        _proc = null;

        if (_container != IntPtr.Zero)
        {
            if (_child != IntPtr.Zero) { _embedder.Release(_child); _child = IntPtr.Zero; }
            _embedder.DestroyContainer(_container);
            _container = IntPtr.Zero;
        }
    }

    public void Dispose() => Stop();
}
