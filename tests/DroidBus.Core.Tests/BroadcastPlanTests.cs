using DroidBus.Core.Control;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class BroadcastPlanTests
{
    [Fact]
    public void Targets_includes_every_online_device_not_just_master()
    {
        // 主控 + 其余在线设备都必须在目标里 —— 这正是放大后广播只命中主控的回归点。
        var targets = BroadcastPlan.Targets(new[]
        {
            ("MASTER", true),
            ("S2", true),
            ("S3", true),
        });
        targets.Should().Equal("MASTER", "S2", "S3");
    }

    [Fact]
    public void Targets_excludes_offline_or_unauthorized_devices()
    {
        var targets = BroadcastPlan.Targets(new[]
        {
            ("S1", true),
            ("S2", false),   // 掉线/掉授权
            ("S3", true),
        });
        targets.Should().Equal("S1", "S3");
    }
}
