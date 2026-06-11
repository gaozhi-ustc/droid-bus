namespace DroidBus.Core.Models;

public enum DeviceState { Online, Offline, Unauthorized }

public sealed class Device
{
    public Device(string serial, DeviceState state)
    {
        Serial = serial;
        State = state;
    }

    public string Serial { get; }
    public DeviceState State { get; set; }
    public string? Model { get; set; }
    public int BatteryPercent { get; set; } = -1;

    public bool IsControllable => State == DeviceState.Online;
}
