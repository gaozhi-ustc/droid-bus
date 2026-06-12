using DroidBus.App.Interop.Linux;
using DroidBus.App.Interop.MacOS;
using DroidBus.App.Interop.Windows;

namespace DroidBus.App.Interop;

public static class EmbedderFactory
{
    /// 按当前操作系统创建对应的窗口嵌入实现。
    public static INativeWindowEmbedder Create()
    {
        if (OperatingSystem.IsWindows()) return new Win32Embedder();
        if (OperatingSystem.IsLinux())   return new X11Embedder();
        if (OperatingSystem.IsMacOS())   return new MacEmbedder();
        throw new PlatformNotSupportedException("未知操作系统。");
    }
}
