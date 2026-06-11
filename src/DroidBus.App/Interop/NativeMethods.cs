using System.Runtime.InteropServices;
using System.Text;

namespace DroidBus.App.Interop;

internal static class NativeMethods
{
    public const int GWL_STYLE = -16;
    public const long WS_CHILD = 0x40000000L;
    public const long WS_POPUP = 0x80000000L;
    public const long WS_CAPTION = 0x00C00000L;
    public const long WS_THICKFRAME = 0x00040000L;
    public const long WS_VISIBLE = 0x10000000L;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("user32.dll", SetLastError = true)] public static extern long GetWindowLongPtr(IntPtr h, int idx);
    [DllImport("user32.dll", SetLastError = true)] public static extern long SetWindowLongPtr(IntPtr h, int idx, long val);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);

    /// 找到属于指定 PID、且标题正好等于 title 的可见顶层窗口。
    /// 必须按标题匹配:并发投屏时,只按 PID 取「第一个可见窗口」会在 scrcpy 初始化阶段
    /// 命中临时窗口而漏掉真正的 SDL 渲染窗,导致嵌入到错误窗口、画面飘出格子。
    public static IntPtr FindWindowByTitleForPid(uint pid, string title)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var wpid);
            if (wpid != pid || !IsWindowVisible(h)) return true;
            var len = GetWindowTextLength(h);
            if (len == 0) return true;
            var sb = new StringBuilder(len + 1);
            GetWindowText(h, sb, sb.Capacity);
            if (sb.ToString() == title) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
