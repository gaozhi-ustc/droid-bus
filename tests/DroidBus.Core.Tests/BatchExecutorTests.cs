using DroidBus.Core.Batch;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class BatchExecutorTests
{
    [Fact]
    public async Task Runs_all_and_aggregates_failures()
    {
        var serials = new[] { "A", "B", "C" };
        var progress = new List<string>();

        var result = await BatchExecutor.RunAsync(
            serials,
            action: async (serial, ct) =>
            {
                await Task.Yield();
                if (serial == "B") throw new InvalidOperationException("boom");
            },
            onProgress: s => { lock (progress) progress.Add(s); });

        result.Succeeded.Should().BeEquivalentTo(new[] { "A", "C" });
        result.Failed.Keys.Should().BeEquivalentTo(new[] { "B" });
        result.Failed["B"].Should().Contain("boom");
        progress.Should().BeEquivalentTo(serials);   // 每台都回报进度一次
    }
}
