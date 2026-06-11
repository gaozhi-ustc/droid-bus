using System.Text;
using DroidBus.Core.Script;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ScriptParserTests
{
    [Fact]
    public void Parses_all_command_types()
    {
        const string script =
            "点击191 832;延时2S;长按163 1416;滑动852 1292 45 1008;" +
            "返回桌面;返回上层;快速点击657 1175;快速双击657 1175;随机延时;" +
            "执行命令input keyevent 24;输入文本hello;ADB文本world;启动应用com.tencent.mobileqq;";

        var cmds = ScriptParser.Parse(script);

        cmds.Should().HaveCount(13);
        cmds[0].Should().BeEquivalentTo(new TapCommand(191, 832));
        cmds[1].Should().BeEquivalentTo(new DelayCommand(TimeSpan.FromSeconds(2)));
        cmds[2].Should().BeEquivalentTo(new LongPressCommand(163, 1416));
        cmds[3].Should().BeEquivalentTo(new SwipeCommand(852, 1292, 45, 1008));
        cmds[4].Should().BeOfType<HomeCommand>();
        cmds[5].Should().BeOfType<BackCommand>();
        cmds[6].Should().BeEquivalentTo(new FastTapCommand(657, 1175));
        cmds[7].Should().BeEquivalentTo(new FastDoubleTapCommand(657, 1175));
        cmds[8].Should().BeOfType<RandomDelayCommand>();
        cmds[9].Should().BeEquivalentTo(new ExecCommand("input keyevent 24"));
        cmds[10].Should().BeEquivalentTo(new InputTextCommand("hello"));
        cmds[11].Should().BeEquivalentTo(new AdbTextCommand("world"));
        cmds[12].Should().BeEquivalentTo(new LaunchAppCommand("com.tencent.mobileqq"));
    }

    [Fact]
    public void Skips_blank_segments_and_supports_newlines()
    {
        ScriptParser.Parse("点击1 2;\n\n返回桌面\n").Should().HaveCount(2);
    }

    [Fact]
    public void Reads_gbk_encoded_file()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("GB2312");
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, gbk.GetBytes("启动应用com.x;返回桌面;"));

        var cmds = ScriptParser.ParseGbkFile(path);

        cmds.Should().HaveCount(2);
        cmds[0].Should().BeEquivalentTo(new LaunchAppCommand("com.x"));
        File.Delete(path);
    }
}
