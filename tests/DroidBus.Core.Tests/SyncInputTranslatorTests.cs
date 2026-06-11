using DroidBus.Core.Control;
using DroidBus.Core.Script;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class SyncInputTranslatorTests
{
    // 格子 360×740,设备 1440×2960 -> 比例 ×4
    [Fact]
    public void Click_at_tile_center_maps_to_device_tap()
    {
        var cmd = SyncInputTranslator.Translate(
            downX: 180, downY: 370, upX: 180, upY: 370,
            tileW: 360, tileH: 740, devW: 1440, devH: 2960);

        cmd.Should().BeEquivalentTo(new TapCommand(720, 1480));
    }

    [Fact]
    public void Drag_maps_to_device_swipe()
    {
        var cmd = SyncInputTranslator.Translate(
            downX: 10, downY: 10, upX: 100, upY: 200,
            tileW: 360, tileH: 740, devW: 1440, devH: 2960);

        cmd.Should().BeEquivalentTo(new SwipeCommand(40, 40, 400, 800));
    }

    [Fact]
    public void Tiny_movement_below_threshold_is_a_tap()
    {
        var cmd = SyncInputTranslator.Translate(50, 50, 53, 52, 360, 740, 1440, 2960);
        cmd.Should().BeOfType<TapCommand>();
    }

    // ---- 流式翻译(Stage 1):捕获的像素采样点序列 -> 归一化 down/move/up 流 ----

    [Fact]
    public void Stream_three_points_yield_down_move_up()
    {
        var pts = new (int, int, long)[] { (0, 0, 100), (200, 400, 108), (400, 800, 116) };

        var stream = SyncInputTranslator.ToPointerStream(pts, contentW: 400, contentH: 800);

        stream.Should().HaveCount(3);
        stream[0].Action.Should().Be(PointerAction.Down);
        stream[1].Action.Should().Be(PointerAction.Move);
        stream[2].Action.Should().Be(PointerAction.Up);
        stream[0].Pressure.Should().Be(1.0f);
        stream[2].Pressure.Should().Be(0f);
        stream[1].NormX.Should().Be(0.5);
        stream[2].TimestampMs.Should().Be(116);
    }

    [Fact]
    public void Stream_single_point_is_down_then_up_in_place()
    {
        var pts = new (int, int, long)[] { (200, 400, 5) };

        var stream = SyncInputTranslator.ToPointerStream(pts, 400, 800);

        stream.Select(s => s.Action).Should().Equal(PointerAction.Down, PointerAction.Up);
        stream[1].NormX.Should().Be(stream[0].NormX);
        stream[1].NormY.Should().Be(stream[0].NormY);
    }

    [Fact]
    public void Stream_empty_points_yield_empty()
    {
        SyncInputTranslator.ToPointerStream(Array.Empty<(int, int, long)>(), 400, 800)
            .Should().BeEmpty();
    }

    [Fact]
    public void Stream_carries_pointer_id_on_every_sample()
    {
        var pts = new (int, int, long)[] { (0, 0, 0), (10, 10, 1) };

        var stream = SyncInputTranslator.ToPointerStream(pts, 400, 800, pointerId: 7);

        stream.Should().OnlyContain(s => s.PointerId == 7);
    }
}
