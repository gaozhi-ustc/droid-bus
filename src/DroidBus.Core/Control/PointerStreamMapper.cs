namespace DroidBus.Core.Control;

/// 归一化指针坐标 ↔ 具体坐标空间的换算,集中一处保真,避免坐标漂移类 bug。
///
/// - 捕获侧 <see cref="Normalize"/>:画面内容区像素(已去黑边,见 <see cref="Letterbox"/>)→ 0..1。
/// - 注入侧 <see cref="ToTarget"/>:0..1 → 目标坐标空间 [0,maxX]×[0,maxY]。
///   scrcpy 用设备显示分辨率(devW/devH);MaaTouch 用握手 `^` 行的触摸原始分辨率
///   max_x/max_y(通常 ≠ 显示分辨率)。两端都做边界钳制。
public static class PointerStreamMapper
{
    /// 画面内容区像素 → 归一化 0..1,越界钳制到单位正方形。内容尺寸非正时返回 (0,0)。
    public static (double NormX, double NormY) Normalize(int px, int py, int contentW, int contentH)
    {
        if (contentW <= 0 || contentH <= 0) return (0.0, 0.0);
        return (Clamp01((double)px / contentW), Clamp01((double)py / contentH));
    }

    /// 归一化 0..1 → 目标坐标 [0,maxX]×[0,maxY],四舍五入并钳制。
    public static (int X, int Y) ToTarget(double normX, double normY, int maxX, int maxY)
        => (Scale(normX, maxX), Scale(normY, maxY));

    private static int Scale(double norm, int max)
    {
        if (max <= 0) return 0;
        var v = (int)System.Math.Round(Clamp01(norm) * max, System.MidpointRounding.AwayFromZero);
        return v < 0 ? 0 : v > max ? max : v;
    }

    private static double Clamp01(double v) => v < 0.0 ? 0.0 : v > 1.0 ? 1.0 : v;
}
