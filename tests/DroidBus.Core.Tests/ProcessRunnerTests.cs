using DroidBus.Core.Process;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task Runs_process_and_captures_stdout()
    {
        var runner = new ProcessRunner();
        var (exe, args) = OperatingSystem.IsWindows()
            ? ("cmd.exe", new[] { "/c", "echo", "hello" })
            : ("/bin/sh", new[] { "-c", "echo hello" });
        var result = await runner.RunAsync(exe, args);
        result.ExitCode.Should().Be(0);
        result.StdOut.Trim().Should().Be("hello");
    }
}
