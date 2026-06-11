using DroidBus.Core.Models;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class DeviceTests
{
    [Fact]
    public void Online_device_is_controllable()
    {
        var d = new Device("29299ad508047ece", DeviceState.Online) { Model = "SM-N960U1", BatteryPercent = 80 };
        d.IsControllable.Should().BeTrue();
        d.Serial.Should().Be("29299ad508047ece");
    }

    [Theory]
    [InlineData(DeviceState.Offline)]
    [InlineData(DeviceState.Unauthorized)]
    public void Non_online_device_is_not_controllable(DeviceState state)
    {
        new Device("x", state).IsControllable.Should().BeFalse();
    }
}
