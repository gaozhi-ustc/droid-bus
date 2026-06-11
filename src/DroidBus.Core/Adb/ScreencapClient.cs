using System.Diagnostics;

namespace DroidBus.Core.Adb;

/// 通过 `adb -s <serial> exec-out screencap -p` 抓取设备屏幕,返回原始 PNG 字节。
/// 用 exec-out + 读 stdout 二进制流(ProcessRunner 按 UTF-8 文本逐行读会破坏 PNG;
/// 普通 `shell screencap` 还会做 LF→CRLF 转换,exec-out 才能拿到干净 PNG)。
public sealed class ScreencapClient
{
    private readonly string _adb;

    public ScreencapClient(string adbPath) => _adb = adbPath;

    public async Task<byte[]> CaptureAsync(string serial, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(_adb)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-s");
        psi.ArgumentList.Add(serial);
        psi.ArgumentList.Add("exec-out");
        psi.ArgumentList.Add("screencap");
        psi.ArgumentList.Add("-p");

        using var p = new System.Diagnostics.Process { StartInfo = psi };
        p.Start();

        using var ms = new MemoryStream();
        var copy = p.StandardOutput.BaseStream.CopyToAsync(ms, ct);
        // 必须把 stderr 读掉,否则它写满管道缓冲会让进程卡死。
        var err = p.StandardError.ReadToEndAsync(ct);
        await copy;
        await err;
        await p.WaitForExitAsync(ct);
        return ms.ToArray();
    }
}
