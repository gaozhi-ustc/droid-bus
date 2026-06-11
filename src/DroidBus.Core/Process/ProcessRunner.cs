using System.Diagnostics;
using System.Text;

namespace DroidBus.Core.Process;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string exe, IReadOnlyList<string> args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = new System.Diagnostics.Process { StartInfo = psi };
        var outBuf = new StringBuilder();
        var errBuf = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) outBuf.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) errBuf.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync(ct);
        return new ProcessResult(p.ExitCode, outBuf.ToString(), errBuf.ToString());
    }
}
