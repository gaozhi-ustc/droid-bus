namespace DroidBus.Core.Process;

/// 长驻进程 + 持有 stdin/stdout 的流式抽象 —— 补 <see cref="IProcessRunner"/> 的空缺:
/// 后者是一次性 run-to-completion,无法持续喂命令。MaaTouch 注入需要一条常驻通道,
/// 边读握手、边持续写 d/m/u/c。用接口让 <see cref="DroidBus.Core.Control.MaaTouchClient"/>
/// 可用 fake 单测,真实现见 StreamingProcess。
public interface IStreamingProcess : IAsyncDisposable
{
    bool IsRunning { get; }

    /// 写一行到 stdin(实现负责追加 \n 并 flush)。
    ValueTask WriteLineAsync(string line, CancellationToken ct = default);

    /// 读 stdout 一行;进程结束/流关闭时返回 null。
    ValueTask<string?> ReadLineAsync(CancellationToken ct = default);
}

/// 创建流式进程(每台设备一个 MaaTouch 会话)。App/MirrorController 用真实工厂,测试用 fake。
public interface IStreamingProcessFactory
{
    IStreamingProcess Start(string exe, IReadOnlyList<string> args);
}
