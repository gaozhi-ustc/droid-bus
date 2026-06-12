namespace DroidBus.App.Interop;

/// 平台窗口嵌入原语(macOS 本期占位)。
public interface INativeWindowEmbedder
{
    /// 在 scrcpy 进程创建窗口后,按 PID+标题找到其窗口句柄。返回 IntPtr.Zero 表示未找到。
    IntPtr FindWindow(int processId, string title);

    /// 将 child 窗口重定父到 host 句柄下,去除装饰使其成为嵌入子窗。
    void Embed(IntPtr child, IntPtr hostHandle);

    /// 移动/缩放已嵌入的 child 窗口。
    void MoveResize(IntPtr child, int x, int y, int width, int height);

    /// 释放嵌入(通常 reparent 回 root 或随进程退出自动销毁)。
    void Release(IntPtr child);

    /// 创建一个嵌入容器(X11: 子窗口;Windows: 无操作,返回 hostHandle 本身)。
    /// 返回的句柄可通过 SDL_WINDOWID 让 scrcpy 直接渲染到此窗口。
    IntPtr CreateEmbedContainer(IntPtr hostHandle, int x, int y, int width, int height);

    /// 移动/缩放容器(用于 ScrcpyHost 的 Resize)。
    void MoveResizeContainer(IntPtr container, int x, int y, int width, int height);

    /// 销毁容器。
    void DestroyContainer(IntPtr container);
}
