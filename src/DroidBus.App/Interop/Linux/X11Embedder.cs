using System.Runtime.InteropServices;
using System.Text;

namespace DroidBus.App.Interop.Linux;

/// Linux/X11 窗口嵌入:通过 XCreateSimpleWindow 创建容器,
/// scrcpy 启动时设 SDL_WINDOWID 使 SDL 直接渲染到该容器,避免 reparent。
public sealed class X11Embedder : INativeWindowEmbedder, IDisposable
{
    private const long XA_CARDINAL = 6;
    private const int Success = 0;

    private readonly IntPtr _display;
    private readonly IntPtr _root;
    private readonly IntPtr _atomNetWmPid;
    private readonly IntPtr _atomNetWmName;
    private readonly IntPtr _atomUtf8String;

    private readonly HashSet<IntPtr> _containers = new();

    private static XErrorHandlerDelegate? _errorHandler;

    public X11Embedder(string? displayName = null)
    {
        _errorHandler = OnXError;
        XSetErrorHandler(_errorHandler);

        _display = XOpenDisplay(displayName);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException(
                $"无法连接 X11 display{(displayName is null ? "" : $" ({displayName})")}。");

        _root = XDefaultRootWindow(_display);
        _atomNetWmPid = XInternAtom(_display, "_NET_WM_PID", false);
        _atomNetWmName = XInternAtom(_display, "_NET_WM_NAME", false);
        _atomUtf8String = XInternAtom(_display, "UTF8_STRING", false);
        DebugLog.Write($"X11Embedder: connected display={_display} root={_root}");
    }

    private static int OnXError(IntPtr display, ref XErrorEvent ev)
    {
        DebugLog.Write($"X11 error (non-fatal): code={ev.error_code} request={ev.request_code} " +
                       $"minor={ev.minor_code} resource=0x{ev.resourceid:X}");
        return 0;
    }

    public IntPtr CreateEmbedContainer(IntPtr hostHandle, int x, int y, int width, int height)
    {
        var container = XCreateSimpleWindow(
            _display, hostHandle,
            x, y, Math.Max(1, width), Math.Max(1, height),
            0, 0, 0);

        XMapWindow(_display, container);
        XRaiseWindow(_display, container);
        XSync(_display, false);

        _containers.Add(container);
        DebugLog.Write($"X11 CreateEmbedContainer: container=0x{container:X} in host=0x{hostHandle:X} at ({x},{y},{width},{height})");
        return container;
    }

    public void MoveResizeContainer(IntPtr container, int x, int y, int width, int height)
    {
        XMoveResizeWindow(_display, container, x, y, Math.Max(1, width), Math.Max(1, height));
        XRaiseWindow(_display, container);
        XFlush(_display);
    }

    public void DestroyContainer(IntPtr container)
    {
        if (_containers.Remove(container))
        {
            XDestroyWindow(_display, container);
            XFlush(_display);
        }
    }

    public void HideContainer(IntPtr container)
    {
        XUnmapWindow(_display, container);
        XFlush(_display);
    }

    public void ShowContainer(IntPtr container)
    {
        XMapWindow(_display, container);
        XRaiseWindow(_display, container);
        XFlush(_display);
    }

    public IntPtr FindWindow(int processId, string title)
    {
        var found = IntPtr.Zero;
        FindRecursive(_root, (uint)processId, title, ref found);
        return found;
    }

    public void Embed(IntPtr child, IntPtr hostHandle)
    {
        DebugLog.Write($"X11 Embed: child=0x{child:X} host=0x{hostHandle:X}");
        XReparentWindow(_display, child, hostHandle, 0, 0);
        XMapWindow(_display, child);
        XRaiseWindow(_display, child);
        XSync(_display, false);
        DebugLog.Write("X11 Embed: done");
    }

    public void MoveResize(IntPtr child, int x, int y, int width, int height)
    {
        XMoveResizeWindow(_display, child, x, y, width, height);
        XRaiseWindow(_display, child);
        XFlush(_display);
    }

    public void Release(IntPtr child)
    {
        XReparentWindow(_display, child, _root, 0, 0);
        XFlush(_display);
    }

    public void Dispose()
    {
        foreach (var container in _containers)
            XDestroyWindow(_display, container);
        _containers.Clear();

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

    [StructLayout(LayoutKind.Sequential)]
    private struct XErrorEvent
    {
        public int type;
        public IntPtr display;
        public nuint resourceid;
        public nuint serial;
        public byte error_code;
        public byte request_code;
        public byte minor_code;
    }

    private delegate int XErrorHandlerDelegate(IntPtr display, ref XErrorEvent ev);

    [DllImport(LibX11)] private static extern IntPtr XSetErrorHandler(XErrorHandlerDelegate handler);
    [DllImport(LibX11)] private static extern IntPtr XOpenDisplay(string? name);
    [DllImport(LibX11)] private static extern int XCloseDisplay(IntPtr display);
    [DllImport(LibX11)] private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(LibX11)]
    private static extern IntPtr XCreateSimpleWindow(
        IntPtr display, IntPtr parent,
        int x, int y, int width, int height, int borderWidth,
        nuint border, nuint background);

    [DllImport(LibX11)] private static extern int XDestroyWindow(IntPtr display, IntPtr window);

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
    [DllImport(LibX11)] private static extern int XUnmapWindow(IntPtr display, IntPtr window);
    [DllImport(LibX11)] private static extern int XRaiseWindow(IntPtr display, IntPtr window);
    [DllImport(LibX11)] private static extern int XFlush(IntPtr display);
    [DllImport(LibX11)] private static extern int XSync(IntPtr display, bool discard);
    [DllImport(LibX11)] private static extern int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, int w, int h);
    [DllImport(LibX11)] private static extern int XFetchName(IntPtr display, IntPtr window, out IntPtr name);
    [DllImport(LibX11)] private static extern int XFree(IntPtr data);
}
