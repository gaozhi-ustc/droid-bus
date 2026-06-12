using System.Runtime.InteropServices;
using System.Text;

namespace DroidBus.App.Interop.Windows;

/// Windows 窗口嵌入(SetParent + WS_CHILD)。
public sealed class Win32Embedder : INativeWindowEmbedder
{
    private const int GWL_STYLE = -16;
    private const long WS_CHILD = 0x40000000L;
    private const long WS_POPUP = 0x80000000L;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;

    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
    [DllImport("user32.dll")] private static extern long GetWindowLongPtr(IntPtr h, int idx);
    [DllImport("user32.dll")] private static extern long SetWindowLongPtr(IntPtr h, int idx, long val);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public IntPtr FindWindow(int processId, string title)
    {
        var ctx = new FindCtx { Pid = (uint)processId, Title = title };
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var wpid);
            if (wpid != ctx.Pid || !IsWindowVisible(h)) return true;
            var len = GetWindowTextLength(h);
            if (len == 0) return true;
            var sb = new StringBuilder(len + 1);
            GetWindowText(h, sb, sb.Capacity);
            if (sb.ToString() == ctx.Title) { ctx.Found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return ctx.Found;
    }

    public void Embed(IntPtr child, IntPtr hostHandle)
    {
        var style = GetWindowLongPtr(child, GWL_STYLE);
        style &= ~WS_POPUP;
        style &= ~WS_CAPTION;
        style &= ~WS_THICKFRAME;
        style |= WS_CHILD | 0x10000000L /* WS_VISIBLE */;
        SetWindowLongPtr(child, GWL_STYLE, style);
        SetParent(child, hostHandle);
    }

    public void MoveResize(IntPtr child, int x, int y, int width, int height)
        => MoveWindow(child, x, y, width, height, true);

    public void Release(IntPtr child) { /* 无需显式释放:parent 销毁时子窗跟着销毁 */ }

    public IntPtr CreateEmbedContainer(IntPtr hostHandle, int x, int y, int width, int height)
        => hostHandle;

    public void MoveResizeContainer(IntPtr container, int x, int y, int width, int height) { }

    public void DestroyContainer(IntPtr container) { }

    private sealed class FindCtx { public uint Pid; public string Title = ""; public IntPtr Found; }
}
