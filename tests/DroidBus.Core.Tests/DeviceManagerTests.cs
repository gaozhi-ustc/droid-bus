using DroidBus.Core.Devices;
using DroidBus.Core.Models;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class DeviceManagerTests
{
    [Fact]
    public void Merge_adds_new_marks_missing_offline_and_reports_changes()
    {
        var current = new List<Device>
        {
            new("A", DeviceState.Online),
            new("B", DeviceState.Online),
        };
        var scan = new List<Device>
        {
            new("A", DeviceState.Online),     // 不变
            new("C", DeviceState.Online),     // 新增
            // B 消失 -> 应标记为 Offline
        };

        var changes = DeviceManager.Merge(current, scan);

        current.Select(d => d.Serial).Should().BeEquivalentTo(new[] { "A", "B", "C" });
        current.Single(d => d.Serial == "B").State.Should().Be(DeviceState.Offline);
        changes.Select(c => c.Serial).Should().BeEquivalentTo(new[] { "B", "C" });
    }
}
