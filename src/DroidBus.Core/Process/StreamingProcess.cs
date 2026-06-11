using System.Diagnostics;
using System.Text;

namespace DroidBus.Core.Process;

/// <see cref="IStreamingProcess"/> 的真实现:包一个长驻 Process,持有 stdin/stdout。
/// stderr 必须持续读掉(否则管道缓冲写满会让进程卡死,同 ScreencapClient 的经验),
/// 故重定向并以事件方式丢弃。命令以 \n 结尾并显式 flush,匹配 minitouch 协议。
public sealed class StreamingProcess : IStreamingProcess
{
    private readonly System.Diagnostics.Process _proc;

    public StreamingProcess(string exe, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        _proc = new System.Diagnostics.Process { StartInfo = psi };
        _proc.ErrorDataReceived += static (_, _) => { /* 丢弃,只为排空管道避免卡死 */ };
        _proc.Start();
        _proc.BeginErrorReadLine();
    }

    public bool IsRunning
    {
        get { try { return !_proc.HasExited; } catch { return false; } }
    }

    public async ValueTask WriteLineAsync(string line, CancellationToken ct = default)
    {
        await _proc.StandardInput.WriteAsync((line + "\n").AsMemory(), ct);
        await _proc.StandardInput.FlushAsync(ct);
    }

    public ValueTask<string?> ReadLineAsync(CancellationToken ct = default)
        => new(_proc.StandardOutput.ReadLineAsync(ct).AsTask());

    public async ValueTask DisposeAsync()
    {
        try { if (!_proc.HasExited) _proc.Kill(true); } catch { /* 已退出/无权,忽略 */ }
        _proc.Dispose();
        await ValueTask.CompletedTask;
    }
}

public sealed class StreamingProcessFactory : IStreamingProcessFactory
{
    public IStreamingProcess Start(string exe, IReadOnlyList<string> args)
        => new StreamingProcess(exe, args);
}
