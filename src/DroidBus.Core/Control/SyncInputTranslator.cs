using DroidBus.Core.Script;

namespace DroidBus.Core.Control;

public static class SyncInputTranslator
{
    private const int TapThresholdPx = 5; // 移动小于该值视为点击

    public static ScriptCommand Translate(
        int downX, int downY, int upX, int upY,
        int tileW, int tileH, int devW, int devH)
    {
        int MapX(int v) => (int)Math.Round((double)v / tileW * devW);
        int MapY(int v) => (int)Math.Round((double)v / tileH * devH);

        var dx = Math.Abs(upX - downX);
        var dy = Math.Abs(upY - downY);
        if (dx <= TapThresholdPx && dy <= TapThresholdPx)
            return new TapCommand(MapX(downX), MapY(downY));

        return new SwipeCommand(MapX(downX), MapY(downY), MapX(upX), MapY(upY));
    }

    /// 把一串捕获到的画面内容区像素采样点(带时间戳)翻译成归一化指针事件流:
    /// 首点 Down、中间点 Move、末点 Up;按下/移动压力 1.0,抬起 0.0。
    /// 单点 → Down + 原地 Up(点击);空序列 → 空。
    ///
    /// 与 <see cref="Translate"/> 不同,这里不做 tap/swipe 归并 —— 保留完整轨迹,
    /// 让下游(scrcpy socket / MaaTouch)逐帧重放,曲线 / 长按拖动 / 惯性滚动不再瞬移。
    public static IReadOnlyList<PointerSample> ToPointerStream(
        IReadOnlyList<(int X, int Y, long TimestampMs)> points,
        int contentW, int contentH,
        long pointerId = 0)
    {
        var result = new List<PointerSample>(points.Count + 1);
        if (points.Count == 0) return result;

        PointerSample At(int i, PointerAction action, float pressure)
        {
            var (nx, ny) = PointerStreamMapper.Normalize(points[i].X, points[i].Y, contentW, contentH);
            return new PointerSample(pointerId, action, nx, ny, pressure, points[i].TimestampMs);
        }

        result.Add(At(0, PointerAction.Down, 1.0f));
        for (var i = 1; i < points.Count - 1; i++)
            result.Add(At(i, PointerAction.Move, 1.0f));

        // 抬起落在末点(单点手势时与按下同位,即一次点击)。
        result.Add(At(points.Count - 1, PointerAction.Up, 0.0f));
        return result;
    }
}
