using DroidBus.Core.Control;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class PointerStreamMapperTests
{
    [Fact]
    public void Normalize_maps_corners_and_center()
    {
        PointerStreamMapper.Normalize(0, 0, 400, 800).Should().Be((0.0, 0.0));
        PointerStreamMapper.Normalize(400, 800, 400, 800).Should().Be((1.0, 1.0));
        PointerStreamMapper.Normalize(200, 400, 400, 800).Should().Be((0.5, 0.5));
    }

    [Fact]
    public void Normalize_clamps_out_of_range_to_unit_square()
    {
        PointerStreamMapper.Normalize(-10, 900, 400, 800).Should().Be((0.0, 1.0));
    }

    [Fact]
    public void Normalize_guards_nonpositive_content()
    {
        PointerStreamMapper.Normalize(10, 10, 0, 800).Should().Be((0.0, 0.0));
        PointerStreamMapper.Normalize(10, 10, 400, -1).Should().Be((0.0, 0.0));
    }

    [Fact]
    public void ToTarget_maps_normalized_to_target_coords()
    {
        // 设备显示分辨率(scrcpy)或触摸原始分辨率(MaaTouch max_x/max_y)都走这一个换算。
        PointerStreamMapper.ToTarget(0.0, 0.0, 1440, 2960).Should().Be((0, 0));
        PointerStreamMapper.ToTarget(1.0, 1.0, 1440, 2960).Should().Be((1440, 2960));
        PointerStreamMapper.ToTarget(0.5, 0.5, 1440, 2960).Should().Be((720, 1480));
    }

    [Fact]
    public void ToTarget_clamps_and_rounds()
    {
        PointerStreamMapper.ToTarget(2.0, -1.0, 1440, 2960).Should().Be((1440, 0));
    }

    [Fact]
    public void Roundtrip_recovers_point_when_capture_matches_device()
    {
        var (nx, ny) = PointerStreamMapper.Normalize(123, 456, 1440, 2960);
        PointerStreamMapper.ToTarget(nx, ny, 1440, 2960).Should().Be((123, 456));
    }
}
