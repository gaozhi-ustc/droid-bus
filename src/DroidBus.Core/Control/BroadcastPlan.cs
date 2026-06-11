namespace DroidBus.Core.Control;

public static class BroadcastPlan
{
    /// 同步输入广播的目标设备:所有在线可控设备(含主控本身)。
    /// 主控也要走 adb —— 捕获层盖在主控画面上,挡住了它自己 scrcpy 的鼠标输入。
    public static IReadOnlyList<string> Targets(IEnumerable<(string Serial, bool Controllable)> devices)
        => devices.Where(d => d.Controllable).Select(d => d.Serial).ToList();
}
