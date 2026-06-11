using DroidBus.Core.Control;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class LetterboxTests
{
    [Fact]
    public void Wider_box_letterboxes_left_right()
    {
        // 面板比竖屏设备更宽:以高为准,左右留黑。设备 1440x2960,面板 800x900。
        var r = Letterbox.Fit(800, 900, 1440, 2960);
        var expectedW = (int)System.Math.Round(900 * 1440.0 / 2960); // 438
        r.Height.Should().Be(900);
        r.Width.Should().Be(expectedW);
        r.OffsetX.Should().Be((800 - expectedW) / 2);
        r.OffsetY.Should().Be(0);
    }

    [Fact]
    public void Taller_box_letterboxes_top_bottom()
    {
        // 面板比设备更高:以宽为准,上下留黑。设备 1440x2960,面板 400x2000。
        var r = Letterbox.Fit(400, 2000, 1440, 2960);
        var expectedH = (int)System.Math.Round(400 * 2960.0 / 1440); // 822
        r.Width.Should().Be(400);
        r.Height.Should().Be(expectedH);
        r.OffsetX.Should().Be(0);
        r.OffsetY.Should().Be((2000 - expectedH) / 2);
    }

    [Fact]
    public void Exact_aspect_has_no_bars()
    {
        var r = Letterbox.Fit(1440, 2960, 1440, 2960);
        r.Should().Be(new Letterbox.Rect(1440, 2960, 0, 0));
    }

    [Fact]
    public void Nonpositive_box_returns_empty()
    {
        Letterbox.Fit(0, 100, 1440, 2960).Should().Be(new Letterbox.Rect(0, 0, 0, 0));
    }
}
