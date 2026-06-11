using DroidBus.Core.Control;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class MaaTouchProtocolTests
{
    [Fact]
    public void ParseHandshake_reads_contacts_max_xy_pressure_version_pid()
    {
        var info = MaaTouchProtocol.ParseHandshake(new[]
        {
            "v 1",
            "^ 10 4096 4096 2048",
            "$ 12345",
        });

        info.MaxContacts.Should().Be(10);
        info.MaxX.Should().Be(4096);
        info.MaxY.Should().Be(4096);
        info.MaxPressure.Should().Be(2048);
        info.Version.Should().Be("1");
        info.Pid.Should().Be(12345);
    }

    [Fact]
    public void ParseHandshake_tolerates_unordered_and_noise_lines()
    {
        var info = MaaTouchProtocol.ParseHandshake(new[]
        {
            "starting maatouch...",
            "^ 2 1080 2280 255",
            "v 1",
        });

        info.MaxContacts.Should().Be(2);
        info.MaxX.Should().Be(1080);
        info.MaxY.Should().Be(2280);
        info.MaxPressure.Should().Be(255);
    }

    [Fact]
    public void Command_builders_format_minitouch_protocol()
    {
        MaaTouchProtocol.Down(0, 100, 200, 50).Should().Be("d 0 100 200 50");
        MaaTouchProtocol.Move(1, 300, 400, 50).Should().Be("m 1 300 400 50");
        MaaTouchProtocol.Up(0).Should().Be("u 0");
        MaaTouchProtocol.Commit().Should().Be("c");
        MaaTouchProtocol.Reset().Should().Be("r");
        MaaTouchProtocol.Wait(16).Should().Be("w 16");
    }

    [Fact]
    public void ToPressure_scales_normalized_to_max_and_clamps()
    {
        MaaTouchProtocol.ToPressure(1.0f, 2048).Should().Be(2048);
        MaaTouchProtocol.ToPressure(0f, 2048).Should().Be(0);
        MaaTouchProtocol.ToPressure(0.5f, 2048).Should().Be(1024);
        MaaTouchProtocol.ToPressure(2.0f, 255).Should().Be(255);
    }
}
