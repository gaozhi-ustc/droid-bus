using DroidBus.Core.Adb;
using DroidBus.Core.Models;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class AdbClientParseTests
{
    [Fact]
    public void Parses_device_lines_with_state_and_model()
    {
        const string output = """
            List of devices attached
            29299ad508047ece       device product:crownqlteue model:SM-N960U1 device:crownqlteue transport_id:5
            525659584b443498       unauthorized
            2620e8b738037ece       offline

            """;

        var devices = AdbClient.ParseDevices(output);

        devices.Should().HaveCount(3);
        devices[0].Serial.Should().Be("29299ad508047ece");
        devices[0].State.Should().Be(DeviceState.Online);
        devices[0].Model.Should().Be("SM-N960U1");
        devices[1].State.Should().Be(DeviceState.Unauthorized);
        devices[2].State.Should().Be(DeviceState.Offline);
    }

    [Fact]
    public void Ignores_header_and_blank_lines()
    {
        AdbClient.ParseDevices("List of devices attached\n\n").Should().BeEmpty();
    }
}
