namespace DroidBus.App.Interop.MacOS;

/// macOS 窗口嵌入占位(本期不实现)。
/// 当前 macOS 上 App 降级为 ScreencapClient 缩略图轮询。
public sealed class MacEmbedder : INativeWindowEmbedder
{
    public IntPtr FindWindow(int processId, string title)
        => throw new PlatformNotSupportedException("macOS 窗口嵌入尚未实现。");

    public void Embed(IntPtr child, IntPtr hostHandle)
        => throw new PlatformNotSupportedException("macOS 窗口嵌入尚未实现。");

    public void MoveResize(IntPtr child, int x, int y, int width, int height)
        => throw new PlatformNotSupportedException("macOS 窗口嵌入尚未实现。");

    public void Release(IntPtr child)
        => throw new PlatformNotSupportedException("macOS 窗口嵌入尚未实现。");

    public IntPtr CreateEmbedContainer(IntPtr hostHandle, int x, int y, int width, int height)
        => throw new PlatformNotSupportedException("macOS 窗口嵌入尚未实现。");

    public void MoveResizeContainer(IntPtr container, int x, int y, int width, int height)
        => throw new PlatformNotSupportedException("macOS 窗口嵌入尚未实现。");

    public void DestroyContainer(IntPtr container)
        => throw new PlatformNotSupportedException("macOS 窗口嵌入尚未实现。");
}
