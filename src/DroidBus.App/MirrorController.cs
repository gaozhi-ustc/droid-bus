using DroidBus.App.Grid;
using DroidBus.App.Mirror;
using DroidBus.Core;
using DroidBus.Core.Mirror;
using DroidBus.Core.Models;

namespace DroidBus.App;

/// 管理每台设备的 ScrcpyHost 生命周期(投屏/停止/崩溃重启)。
public sealed class MirrorController : IDisposable
{
    private readonly BinaryLocator _bin;
    private readonly int _devW;
    private readonly int _devH;
    private readonly Dictionary<string, ScrcpyHost> _hosts = new();
    private readonly Dictionary<string, int> _retries = new();
    private const int MaxRetries = 3;

    // 由 MainForm 注入:把「重投某 serial」的动作回调进来。
    public Func<string, Task>? RestartRequested { get; set; }

    public MirrorController(BinaryLocator bin, int devW, int devH)
    {
        _bin = bin;
        _devW = devW;
        _devH = devH;
    }

    public bool IsMirroring(string serial) => _hosts.ContainsKey(serial);

    /// 对一个 tile 启动投屏;若已在投屏则忽略。崩溃时自动重启(Task 23 增强)。
    public async Task StartAsync(DeviceTile tile, MirrorOptions options)
    {
        if (tile.Device is not { } dev || !dev.IsControllable) return;
        if (_hosts.ContainsKey(dev.Serial)) return;

        var host = new ScrcpyHost(_bin, tile.Surface, _devW, _devH);
        _hosts[dev.Serial] = host;
        host.Crashed += code =>
        {
            var surface = tile.Surface;
            if (surface.IsHandleCreated)
                surface.BeginInvoke(new Action(async () => await HandleCrashAsync(dev.Serial)));
            // 句柄已销毁(窗体关闭中)则忽略;Dispose 会负责杀进程
        };
        try
        {
            await host.StartAsync(dev, options);
        }
        catch
        {
            _hosts.Remove(dev.Serial);
            host.Dispose();
            throw;
        }
        _retries[dev.Serial] = 0;
    }

    private async Task HandleCrashAsync(string serial)
    {
        if (!_hosts.ContainsKey(serial)) return; // 已被主动 Stop
        var n = _retries.GetValueOrDefault(serial);
        if (n >= MaxRetries) return;
        _retries[serial] = n + 1;
        _hosts.Remove(serial, out var dead); dead?.Dispose();
        await Task.Delay(500 * (n + 1));
        if (RestartRequested is not null) await RestartRequested(serial);
    }

    /// 重新投屏一台(用于切换单台开关后重启 scrcpy)。
    public async Task RestartAsync(DeviceTile tile, MirrorOptions options)
    {
        Stop(tile.Device?.Serial);
        await StartAsync(tile, options);
    }

    public void Stop(string? serial)
    {
        if (serial is null) return;
        _retries.Remove(serial);
        if (_hosts.Remove(serial, out var host)) host.Dispose();
    }

    public void ResizeAll()
    {
        foreach (var h in _hosts.Values) h.Resize();
    }

    public ScrcpyHost? Get(string serial) => _hosts.TryGetValue(serial, out var h) ? h : null;

    public void Dispose()
    {
        foreach (var h in _hosts.Values) h.Dispose();
        _hosts.Clear();
    }
}
