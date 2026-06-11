using System.Text.RegularExpressions;
using DroidBus.Core.Models;
using DroidBus.Core.Process;

namespace DroidBus.Core.Adb;

public sealed class AdbClient
{
    private readonly IProcessRunner _runner;
    private readonly string _adb;

    public AdbClient(IProcessRunner runner, string adbPath)
    {
        _runner = runner;
        _adb = adbPath;
    }

    public static IReadOnlyList<Device> ParseDevices(string adbDevicesOutput)
    {
        var list = new List<Device>();
        foreach (var raw in adbDevicesOutput.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("List of devices")) continue;

            var parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var serial = parts[0];
            var rest = parts[1].Trim();
            var state = rest.StartsWith("device")
                ? DeviceState.Online
                : rest.StartsWith("unauthorized") ? DeviceState.Unauthorized : DeviceState.Offline;

            var device = new Device(serial, state);
            var m = Regex.Match(rest, @"model:(\S+)");
            if (m.Success) device.Model = m.Groups[1].Value;
            list.Add(device);
        }
        return list;
    }

    public async Task<IReadOnlyList<Device>> ListDevicesAsync(CancellationToken ct = default)
    {
        var r = await _runner.RunAsync(_adb, new[] { "devices", "-l" }, ct);
        return ParseDevices(r.StdOut);
    }

    public async Task<int> GetBatteryAsync(string serial, CancellationToken ct = default)
    {
        var r = await _runner.RunAsync(_adb,
            new[] { "-s", serial, "shell", "dumpsys", "battery" }, ct);
        var m = Regex.Match(r.StdOut, @"level:\s*(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }
}
