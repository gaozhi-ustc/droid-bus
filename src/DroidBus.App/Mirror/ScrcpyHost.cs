using System.Diagnostics;
using System.Threading;
using DroidBus.App.Interop;
using DroidBus.Core;
using DroidBus.Core.Mirror;
using DroidBus.Core.Models;

namespace DroidBus.App.Mirror;

public sealed class ScrcpyHost : IDisposable
{
    private readonly BinaryLocator _bin;
    private readonly Control _parent;     // 承载的 Panel
    private readonly int _devW;
    private readonly int _devH;
    private Process? _proc;
    private IntPtr _child = IntPtr.Zero;
    private EventHandler? _resizeHandler;

    public ScrcpyHost(BinaryLocator bin, Control parent, int devW, int devH)
    {
        _bin = bin;
        _parent = parent;
        _devW = devW;
        _devH = devH;
    }

    private int _crashedRaised;

    public string? Serial { get; private set; }
    /// scrcpy 进程意外退出(崩溃/设备掉线)时触发,参数为退出码。
    public event Action<int>? Crashed;

    private void RaiseCrashed(int code)
    {
        if (Interlocked.Exchange(ref _crashedRaised, 1) == 0)
            Crashed?.Invoke(code);
    }

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
            if (code != 0) RaiseCrashed(code);
        };
        _proc.Start();

        // 轮询等待带标题的 scrcpy 渲染窗出现(最多 ~15 秒;并发投屏时窗口创建会变慢)
        for (var i = 0; i < 150 && _child == IntPtr.Zero; i++)
        {
            await Task.Delay(100);
            _proc.Refresh();
            if (_proc.HasExited) { RaiseCrashed(_proc.ExitCode); return; }
            _child = NativeMethods.FindWindowByTitleForPid((uint)_proc.Id, title);
        }
        if (_child == IntPtr.Zero)
            throw new InvalidOperationException($"未能定位 {device.Serial} 的 scrcpy 窗口");

        Embed();
    }

    private void Embed()
    {
        if (_child == IntPtr.Zero) return;
        // 改成无边框子窗口
        var style = NativeMethods.GetWindowLongPtr(_child, NativeMethods.GWL_STYLE);
        style &= ~NativeMethods.WS_POPUP;
        style &= ~NativeMethods.WS_CAPTION;
        style &= ~NativeMethods.WS_THICKFRAME;
        style |= NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE;
        NativeMethods.SetWindowLongPtr(_child, NativeMethods.GWL_STYLE, style);

        NativeMethods.SetParent(_child, _parent.Handle);
        // 跟随承载面板尺寸变化(网格↔大主控切换、窗口缩放),不再依赖外部恰好的时机调用 Resize。
        _resizeHandler = (_, _) => Resize();
        _parent.SizeChanged += _resizeHandler;
        Resize();
    }

    public void Resize()
    {
        if (_child == IntPtr.Zero) return;
        // 把 scrcpy 窗按设备宽高比居中放进承载面板:窗口宽高比即画面宽高比,scrcpy 边到边填满,
        // 不再自行 letterbox 把画面缩到角落。广播侧用同一 Letterbox.Fit 反算坐标,二者严格对齐。
        var box = DroidBus.Core.Control.Letterbox.Fit(
            _parent.ClientSize.Width, _parent.ClientSize.Height, _devW, _devH);
        if (box.Width <= 0 || box.Height <= 0) return;
        NativeMethods.MoveWindow(_child, box.OffsetX, box.OffsetY, box.Width, box.Height, true);
    }

    public void Stop()
    {
        if (_resizeHandler != null) { _parent.SizeChanged -= _resizeHandler; _resizeHandler = null; }
        try { if (_proc is { HasExited: false }) _proc.Kill(true); } catch { }
        _proc?.Dispose();
        _proc = null;
        _child = IntPtr.Zero;
    }

    public void Dispose() => Stop();
}
