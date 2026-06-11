using System.Drawing.Drawing2D;
using DroidBus.App.Grid;
using DroidBus.Core.Adb;

namespace DroidBus.App.Mirror;

/// 主控模式下,对缩略条里的设备低频 adb screencap 抓图刷新预览。
/// scrcpy 窗口被跨进程缩到极小尺寸后不再重绘(预览全黑),抓图绕开这个问题。
public sealed class ThumbnailPoller : IDisposable
{
    private readonly ScreencapClient _cap;
    private readonly TimeSpan _gap;
    private CancellationTokenSource? _cts;

    public ThumbnailPoller(string adbPath, TimeSpan gap)
    {
        _cap = new ScreencapClient(adbPath);
        _gap = gap;
    }

    /// 开始对这批 tile 轮询抓图;会先停掉上一轮。
    /// 单条循环逐台轮流抓:任一时刻只有一张截图在走 adb server,
    /// 把中转通道让给主控 scrcpy 视频流和广播 input 指令,避免整机卡顿。
    /// (单台 screencap 约 800ms 且是设备端 PNG 编码耗时,5 台并发只会互相把 adb server 挤爆。)
    public void Start(IReadOnlyList<DeviceTile> tiles)
    {
        Stop();
        if (tiles.Count == 0) return;
        _cts = new CancellationTokenSource();
        _ = LoopAsync(tiles.ToArray(), _cts.Token);
    }

    private async Task LoopAsync(DeviceTile[] tiles, CancellationToken ct)
    {
        int i = 0;
        while (!ct.IsCancellationRequested)
        {
            await RefreshAsync(tiles[i], ct);
            i = (i + 1) % tiles.Length;
            try { await Task.Delay(_gap, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RefreshAsync(DeviceTile tile, CancellationToken ct)
    {
        var serial = tile.Device?.Serial;
        if (serial is null) return;
        try
        {
            var png = await _cap.CaptureAsync(serial, ct);
            if (png.Length == 0 || ct.IsCancellationRequested) return;
            var bmp = DecodeScaled(png, maxHeight: 400);
            if (bmp is null) return;
            if (ct.IsCancellationRequested || !tile.IsHandleCreated) { bmp.Dispose(); return; }
            tile.BeginInvoke(new Action(() => tile.ShowThumbnail(bmp)));
        }
        catch { /* 单台抓图/解码失败忽略,下一拍再试 */ }
    }

    /// 解码 PNG 并按比例缩到 maxHeight 以内,降低 GDI 内存与刷新开销。
    private static Bitmap? DecodeScaled(byte[] png, int maxHeight)
    {
        using var ms = new MemoryStream(png);
        using var src = new Bitmap(ms);
        var scale = Math.Min(1.0, (double)maxHeight / src.Height);
        var w = Math.Max(1, (int)(src.Width * scale));
        var h = Math.Max(1, (int)(src.Height * scale));
        var dst = new Bitmap(w, h);
        using var g = Graphics.FromImage(dst);
        // 缩略图用 Bilinear:画质足够,远快于 HighQualityBicubic,避免每帧解码成为瓶颈。
        g.InterpolationMode = InterpolationMode.Bilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(src, 0, 0, w, h);
        return dst;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();
}
