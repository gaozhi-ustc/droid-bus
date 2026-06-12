using DroidBus.Core.Mirror;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ScrcpyArgsBuilderTests
{
    [Fact]
    public void Default_options_produce_base_args()
    {
        var args = ScrcpyArgsBuilder.Build("S1", new MirrorOptions());
        args.Should().Contain(new[] { "-s", "S1" });
        args.Should().Contain("--window-borderless");
        args.Should().Contain("--no-audio");                 // A10 默认关音频
        args.Should().Contain("--window-title");
        args.Should().Contain("DroidBus:S1");
        args.Should().Contain("--video-bit-rate");
        args.Should().Contain("4M");
        args.Should().Contain("--max-size");
        args.Should().Contain("1080");
        // 默认软件渲染:整合显卡 + 远程/虚拟显示器下,6 路并发 Direct3D 会黑屏(抢不到 GPU 渲染资源)
        args.Should().Contain("--render-driver");
        args.Should().Contain("software");
    }

    [Fact]
    public void Render_driver_is_configurable_and_omitted_when_blank()
    {
        ScrcpyArgsBuilder.Build("S1", new MirrorOptions { RenderDriver = "opengl" }).Should().Contain("opengl");
        ScrcpyArgsBuilder.Build("S1", new MirrorOptions { RenderDriver = "" }).Should().NotContain("--render-driver");
    }

    [Fact]
    public void WindowTitle_is_DroidBus_prefixed_serial_and_used_by_build()
    {
        ScrcpyArgsBuilder.WindowTitle("ABC123").Should().Be("DroidBus:ABC123");
        // Build 必须用同一个标题,嵌入端才能按标题精确定位窗口
        var args = ScrcpyArgsBuilder.Build("ABC123", new MirrorOptions());
        args.Should().Contain(ScrcpyArgsBuilder.WindowTitle("ABC123"));
    }

    [Fact]
    public void Toggles_add_flags()
    {
        var recDir = Path.Combine(Path.GetTempPath(), "rec");
        var o = new MirrorOptions
        {
            TurnScreenOff = true, StayAwake = true, ShowTouches = true,
            LockOrientation = 0, BitRateMbps = 8, MaxSize = 1280,
            Record = true, RecordDir = recDir
        };
        var args = ScrcpyArgsBuilder.Build("S1", o);
        args.Should().Contain("--turn-screen-off");
        args.Should().Contain("--stay-awake");
        args.Should().Contain("--show-touches");
        args.Should().Contain("--lock-video-orientation");
        args.Should().Contain("0");
        args.Should().Contain("8M");
        args.Should().Contain("1280");
        // record 路径形如 <recDir>/S1-<timestamp>.mp4(跨平台用 Path.Combine 还原前缀)
        var recPrefix = Path.Combine(recDir, "S1-");
        args.Should().Contain(a => a.StartsWith(recPrefix) && a.EndsWith(".mp4"));
    }
}
