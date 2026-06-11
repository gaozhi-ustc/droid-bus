namespace DroidBus.Core.Control;

/// 把宿主机键盘事件翻译成设备动作。纯函数、无 WinForms 依赖、可单测。
///
/// 分工避免重复触发:
/// - 可打印字符(含 IME 合成的中文,经 KeyPress/WM_CHAR 交付)走 <see cref="FromChar"/> → 文本;
/// - 特殊/导航/编辑键(经 KeyDown 交付,字符是控制字符或无字符)走 <see cref="FromVirtualKey"/> → keyevent。
/// 控制字符(\r \b \t)在 FromChar 里被忽略,改由 FromVirtualKey 出 keyevent,二者不重叠。
public static class KeyTranslator
{
    // Windows 虚拟键码(= WinForms Keys 枚举值)→ Android keycode,仅特殊/导航/编辑键。
    private static readonly IReadOnlyDictionary<int, int> SpecialKeys = new Dictionary<int, int>
    {
        [0x0D] = 66,   // VK_RETURN -> KEYCODE_ENTER
        [0x08] = 67,   // VK_BACK   -> KEYCODE_DEL(退格)
        [0x09] = 61,   // VK_TAB    -> KEYCODE_TAB
        [0x1B] = 111,  // VK_ESCAPE -> KEYCODE_ESCAPE
        [0x25] = 21,   // VK_LEFT   -> KEYCODE_DPAD_LEFT
        [0x26] = 19,   // VK_UP     -> KEYCODE_DPAD_UP
        [0x27] = 22,   // VK_RIGHT  -> KEYCODE_DPAD_RIGHT
        [0x28] = 20,   // VK_DOWN   -> KEYCODE_DPAD_DOWN
        [0x2E] = 112,  // VK_DELETE -> KEYCODE_FORWARD_DEL
        [0x24] = 122,  // VK_HOME   -> KEYCODE_MOVE_HOME
        [0x23] = 123,  // VK_END    -> KEYCODE_MOVE_END
        [0x21] = 92,   // VK_PRIOR  -> KEYCODE_PAGE_UP
        [0x22] = 93,   // VK_NEXT   -> KEYCODE_PAGE_DOWN
    };

    /// 可打印字符 → 文本注入;控制字符 → null(交给 <see cref="FromVirtualKey"/>)。
    public static KeyAction? FromChar(char c)
        => char.IsControl(c) ? null : new TypeTextAction(c.ToString());

    /// Windows 虚拟键码 → 特殊键 keyevent;普通字符键 → null(交给 <see cref="FromChar"/>)。
    public static KeyAction? FromVirtualKey(int virtualKeyCode)
        => SpecialKeys.TryGetValue(virtualKeyCode, out var code) ? new KeyEventAction(code) : null;
}
