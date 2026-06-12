using DroidBus.App.Interop;
using DroidBus.App.Mirror;
using DroidBus.App.Views;
using DroidBus.Core;
using DroidBus.Core.Mirror;
using DroidBus.Core.Models;

namespace DroidBus.App;

/// 管理每台设备的 ScrcpyHost 生命周期(投屏/停止/崩溃重启),平台无关。
public sealed class MirrorController : IDisposable
{
    private readonly BinaryLocator _bin;
    private readonly INativeWindowEmbedder _embedder;
    private readonly int _devW;
    private readonly int _devH;
    private readonly Dictionary<string, ScrcpyHost> _hosts = new();
    private readonly Dictionary<string, DeviceTile> _tileBySerial = new();
    private readonly Dictionary<string, int> _retries = new();
    private const int MaxRetries = 3;

    /// 顶层窗口的平台句柄(Windows=HWND, Linux=X11 Window),由 MainWindow 设置。
    public IntPtr WindowHandle { get; set; }

    public Func<string, Task>? RestartRequested { get; set; }

    public MirrorController(BinaryLocator bin, INativeWindowEmbedder embedder, int devW, int devH)
    {
        _bin = bin;
        _embedder = embedder;
        _devW = devW;
        _devH = devH;
    }

    public bool IsMirroring(string serial) => _hosts.ContainsKey(serial);

    /// 对一台 tile 启动投屏;若已在投屏则忽略。
    public async Task StartAsync(DeviceTile tile, MirrorOptions options)
    {
        if (tile.Device is not { } dev || !dev.IsControllable) return;
        if (_hosts.ContainsKey(dev.Serial)) return;

        var host = new ScrcpyHost(_bin, _embedder, _devW, _devH);
        _hosts[dev.Serial] = host;
        _tileBySerial[dev.Serial] = tile;

        // 若顶层窗口句柄已就绪,立即传给 host(否则等 resize 时再传)
        if (WindowHandle != IntPtr.Zero)
            host.SetHostHandle(WindowHandle);

        host.Crashed += async code => await HandleCrashAsync(dev.Serial);

        try
        {
            await host.StartAsync(dev, options);
        }
        catch
        {
            _hosts.Remove(dev.Serial);
            _tileBySerial.Remove(dev.Serial);
            host.Dispose();
            throw;
        }
        _retries[dev.Serial] = 0;
    }

    private async Task HandleCrashAsync(string serial)
    {
        if (!_hosts.ContainsKey(serial)) return;
        var n = _retries.GetValueOrDefault(serial);
        if (n >= MaxRetries) return;
        _retries[serial] = n + 1;
        _hosts.Remove(serial, out var dead); dead?.Dispose();
        await Task.Delay(500 * (n + 1));
        if (RestartRequested is not null) await RestartRequested(serial);
    }

    public async Task RestartAsync(DeviceTile tile, MirrorOptions options)
    {
        Stop(tile.Device?.Serial);
        await StartAsync(tile, options);
    }

    public void Stop(string? serial)
    {
        if (serial is null) return;
        _retries.Remove(serial);
        _tileBySerial.Remove(serial);
        if (_hosts.Remove(serial, out var host)) host.Dispose();
    }

    /// 对所有正在投屏的设备重新计算窗口尺寸。
    public void ResizeAll()
    {
        foreach (var (serial, host) in _hosts)
        {
            if (_tileBySerial.TryGetValue(serial, out var tile))
            {
                var w = (int)tile.Bounds.Width;
                var h = (int)tile.Bounds.Height - 30;
                if (w > 0 && h > 0) host.Resize(w, h);
            }
        }
    }

    public ScrcpyHost? Get(string serial) => _hosts.TryGetValue(serial, out var h) ? h : null;

    public void Dispose()
    {
        foreach (var h in _hosts.Values) h.Dispose();
        _hosts.Clear();
        _tileBySerial.Clear();
    }
}
