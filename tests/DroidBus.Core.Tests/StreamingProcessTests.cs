using DroidBus.Core.Process;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class StreamingProcessTests
{
    // 真进程验证流式通道:写一行 stdin -> 读回 stdout。用 powershell 作 line-echo,
    // 证明持久进程 + 持有 stdin/stdout 的机制工作(MaaTouch 真机注入即靠这条通道)。
    [Fact]
    public async Task Streams_lines_through_a_real_process()
    {
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;
        var factory = new StreamingProcessFactory();
        await using var p = factory.Start("powershell", new[]
        {
            "-NoProfile", "-Command",
            "while($l=[Console]::In.ReadLine()){[Console]::Out.WriteLine('echo:'+$l)}"
        });

        await p.WriteLineAsync("hello", ct);
        var line = await p.ReadLineAsync(ct);

        line.Should().Be("echo:hello");
    }
}
