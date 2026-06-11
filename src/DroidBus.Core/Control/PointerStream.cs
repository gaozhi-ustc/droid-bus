namespace DroidBus.Core.Control;

/// 指针事件动作,刻意对齐 Android MotionEvent 的 DOWN/MOVE/UP,
/// 使其能 1:1 翻译到 scrcpy INJECT_TOUCH_EVENT(action 字段)或 MaaTouch 的 d/m/u 命令。
public enum PointerAction { Down, Move, Up, Cancel }

/// 一个与传输无关、与设备分辨率无关的指针采样点。
///
/// 坐标用 0..1 归一化(相对设备画面内容区,letterbox 黑边已扣),
/// 这样旋转 / DPI / 窗口缩放 / 异分辨率从机的差异,只在末端注入时一次性映射,
/// 同一条流即可喂给分辨率不同的多台从机。
public readonly record struct PointerSample(
    long PointerId,
    PointerAction Action,
    double NormX,
    double NormY,
    float Pressure,
    long TimestampMs);

/// 一台目标设备的注入下游。scrcpy 控制 socket / MaaTouch / adb 兜底各实现一份;
/// 群控广播 = 把同一条 PointerSample 流 Emit 给 N 个 sink。
public interface IPointerSink
{
    ValueTask EmitAsync(PointerSample sample, CancellationToken ct = default);
}
