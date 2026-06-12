using System.Runtime.InteropServices;
using System.Text;

namespace DroidBus.App.Interop.Linux;

/// Linux/X11 窗口嵌入(XReparentWindow)。
/// 打开 X11 连接并遍历窗口树,按 _NET_WM_PID + 标题匹配 scrcpy 窗口,重定父到 Avalonia NativeControlHost。
public sealed class X11Embedder : INativeWindowEmbedder, IDisposable
{
    // ---- X11 常量 ----------------------------------------------------------
    private const long XA_CARDINAL = 6;
    private const long AnyPropertyType = 0;
    private const int Success = 0;

    /// X11 底层类型:Window=nuint, Display*=IntPtr, Atom=nuint。用 IntPtr/nint 兼容。
    private readonly IntPtr _display;
    private readonly IntPtr _root;              // XDefaultRootWindow
    private readonly IntPtr _atomNetWmPid;
    private readonly IntPtr _atomNetWmName;
    private readonly IntPtr _atomUtf8String;

    public X11Embedder(string? displayName = null)
    {
        _display = XOpenDisplay(displayName);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException(
                $"无法连接 X11 display{(displayName is null ? "" : $" ({displayName})")}。");

        _root = XDefaultRootWindow(_display);
        _atomNetWmPid = XInternAtom(_display, "_NET_WM_PID", false);
        _atomNetWmName = XInternAtom(_display, "_NET_WM_NAME", false);
        _atomUtf8String = XInternAtom(_display, "UTF8_STRING", false);
    }

    public IntPtr FindWindow(int processId, string title)
    {
        var found = IntPtr.Zero;
        FindRecursive(_root, (uint)processId, title, ref found);
        return found;
    }

    public void Embed(IntPtr child, IntPtr hostHandle)
    {
        var ret = XReparentWindow(_display, child, hostHandle, 0, 0);
        if (ret != Success)
            throw new InvalidOperationException("XReparentWindow 失败。");
        XMapWindow(_display, child);
    }

    public void MoveResize(IntPtr child, int x, int y, int width, int height)
    {
        XMoveResizeWindow(_display, child, x, y, width, height);
    }

    public void Release(IntPtr child)
    {
        // 把窗口放回 root(即取消嵌入);进程退出时系统自动回收。
        XReparentWindow(_display, child, _root, 0, 0);
    }

    public void Dispose()
    {
        if (_display != IntPtr.Zero) XCloseDisplay(_display);
    }

    // ---- 窗口查找递归 -------------------------------------------------------
    private void FindRecursive(IntPtr parent, uint targetPid, string title, ref IntPtr found)
    {
        if (found != IntPtr.Zero) return;

        XQueryTree(_display, parent, out _, out _, out var children, out var n);
        if (children == IntPtr.Zero || n <= 0) return;

        var childPtrs = new IntPtr[n];
        Marshal.Copy(children, childPtrs, 0, n);

        foreach (var child in childPtrs)
        {
            if (found != IntPtr.Zero) break;

            var pid = ReadWmPid(child);
            if (pid != targetPid) { FindRecursive(child, targetPid, title, ref found); continue; }

            var name = ReadWindowName(child);
            if (name != title) { FindRecursive(child, targetPid, title, ref found); continue; }

            found = child;
        }
        XFree(children);
    }

    private uint? ReadWmPid(IntPtr window)
    {
        if (XGetWindowProperty(_display, window, _atomNetWmPid,
                0, 1, false, new IntPtr(XA_CARDINAL),
                out var type, out var format, out var nitems, out _, out var data) != Success)
            return null;
        if (type == IntPtr.Zero || data == IntPtr.Zero || nitems == 0) return null;
        var pid = (uint)Marshal.ReadInt32(data);
        XFree(data);
        return pid;
    }

    private string? ReadWindowName(IntPtr window)
    {
        // 优先 _NET_WM_NAME(UTF8_STRING)
        if (XGetWindowProperty(_display, window, _atomNetWmName,
                0, 512, false, _atomUtf8String,
                out var type, out var format, out var nitems, out _, out var data) == Success
            && type != IntPtr.Zero && data != IntPtr.Zero && nitems > 0 && format == 8)
        {
            var bytes = new byte[nitems];
            Marshal.Copy(data, bytes, 0, (int)nitems);
            XFree(data);
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }
        if (data != IntPtr.Zero) XFree(data);

        // 回退 WM_NAME(XFetchName,旧式 Latin-1)
        if (XFetchName(_display, window, out var namePtr) == Success && namePtr != IntPtr.Zero)
        {
            var s = Marshal.PtrToStringAnsi(namePtr);
            XFree(namePtr);
            return s;
        }
        return null;
    }

    // ---- P/Invoke ----------------------------------------------------------
    private const string LibX11 = "libX11.so.6";

    [DllImport(LibX11)] private static extern IntPtr XOpenDisplay(string? name);
    [DllImport(LibX11)] private static extern int XCloseDisplay(IntPtr display);
    [DllImport(LibX11)] private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(LibX11)]
    private static extern int XQueryTree(IntPtr display, IntPtr window,
        out IntPtr root, out IntPtr parent, out IntPtr children, out int nchildren);

    [DllImport(LibX11)]
    private static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
        long offset, long length, bool delete, IntPtr reqType,
        out IntPtr actualType, out int actualFormat, out long nitems, out long bytesAfter, out IntPtr prop);

    [DllImport(LibX11)] private static extern IntPtr XInternAtom(IntPtr display, string name, bool onlyIfExists);
    [DllImport(LibX11)] private static extern int XReparentWindow(IntPtr display, IntPtr window, IntPtr parent, int x, int y);
    [DllImport(LibX11)] private static extern int XMapWindow(IntPtr display, IntPtr window);
    [DllImport(LibX11)] private static extern int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, int w, int h);
    [DllImport(LibX11)] private static extern int XFetchName(IntPtr display, IntPtr window, out IntPtr name);
    [DllImport(LibX11)] private static extern int XFree(IntPtr data);
}
