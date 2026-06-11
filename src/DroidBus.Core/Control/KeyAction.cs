namespace DroidBus.Core.Control;

/// 一次宿主键盘输入翻译后的设备动作。两种:Unicode 文本(走 ADBKeyboard)或
/// 特殊键 keyevent(走 input keyevent)。与传输无关,App 侧据此路由到目标设备。
public abstract record KeyAction;

/// 可打印字符(含 IME 合成的中文)→ 文本注入。
public sealed record TypeTextAction(string Text) : KeyAction;

/// 特殊/导航/编辑键 → Android keyevent。
public sealed record KeyEventAction(int AndroidKeyCode) : KeyAction;
