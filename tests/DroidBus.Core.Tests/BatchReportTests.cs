using DroidBus.Core.Batch;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class BatchReportTests
{
    [Fact]
    public void All_success_reports_count()
    {
        var r = new BatchResult(
            Succeeded: new List<string> { "S1", "S2" },
            Failed: new Dictionary<string, string>());
        BatchReport.Summarize(r).Should().Be("成功 2 台,失败 0 台。");
    }

    [Fact]
    public void Failures_listed_with_reason()
    {
        var r = new BatchResult(
            Succeeded: new List<string> { "S1" },
            Failed: new Dictionary<string, string> { ["S2"] = "install failed" });
        BatchReport.Summarize(r).Should().Be(
            "成功 1 台,失败 1 台。\n失败设备:\n  S2: install failed");
    }
}
