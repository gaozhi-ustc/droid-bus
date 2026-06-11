namespace DroidBus.Core.Control;

/// scrcpy 在承载窗口内按设备宽高比居中显示画面,四周留黑边(letterbox)。
/// 同步输入广播时,捕获到的是承载面板坐标,必须先换算到真正的画面区域,
/// 否则点位会整体偏移、缩放错乱。
public static class Letterbox
{
    public readonly record struct Rect(int Width, int Height, int OffsetX, int OffsetY);

    /// 在 box(boxW×boxH)内按 dev 宽高比居中放置画面:返回画面尺寸与左/上黑边偏移。
    public static Rect Fit(int boxW, int boxH, int devW, int devH)
    {
        if (boxW <= 0 || boxH <= 0 || devW <= 0 || devH <= 0) return new Rect(0, 0, 0, 0);

        double boxAspect = (double)boxW / boxH;
        double devAspect = (double)devW / devH;

        int w, h;
        if (boxAspect > devAspect)        // 面板更宽 → 以高为准,左右留黑
        {
            h = boxH;
            w = (int)System.Math.Round(boxH * devAspect);
        }
        else                              // 面板更高(或等比)→ 以宽为准,上下留黑
        {
            w = boxW;
            h = (int)System.Math.Round(boxW / devAspect);
        }
        return new Rect(w, h, (boxW - w) / 2, (boxH - h) / 2);
    }
}
