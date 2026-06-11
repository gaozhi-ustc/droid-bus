using DroidBus.Core.Audio;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class SndcpyCommandsTests
{
    [Fact]
    public void Install_args_target_serial()
    {
        SndcpyCommands.Install("S1", @"C:\r\sndcpy.apk")
            .Should().Equal("-s", "S1", "install", "-r", @"C:\r\sndcpy.apk");
    }

    [Fact]
    public void Forward_sets_localabstract_socket()
    {
        SndcpyCommands.Forward("S1", 28200)
            .Should().Equal("-s", "S1", "forward", "tcp:28200", "localabstract:sndcpy");
    }

    [Fact]
    public void StartService_uses_am_start_foreground_service()
    {
        SndcpyCommands.StartService("S1")
            .Should().Equal("-s", "S1", "shell", "am", "start-foreground-service",
                "com.rom1v.sndcpy/.RecordService");
    }
}
