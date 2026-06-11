using DroidBus.Core.Adb;
using DroidBus.Core.Models;

namespace DroidBus.Core.Devices;

public sealed class DeviceManager
{
    private readonly AdbClient _adb;
    private readonly List<Device> _devices = new();

    public DeviceManager(AdbClient adb) => _adb = adb;

    public IReadOnlyList<Device> Devices => _devices;

    /// 某台设备状态发生变化时触发(新增 / 上线 / 掉线 / 掉授权)。
    public event Action<Device>? DeviceChanged;

    /// 把一次扫描结果并入 current,返回发生变化的设备。静态以便纯单测。
    public static IReadOnlyList<Device> Merge(List<Device> current, IReadOnlyList<Device> scan)
    {
        var changed = new List<Device>();
        var scanSerials = scan.Select(s => s.Serial).ToHashSet();

        foreach (var s in scan)
        {
            var existing = current.FirstOrDefault(d => d.Serial == s.Serial);
            if (existing is null)
            {
                current.Add(s);
                changed.Add(s);
            }
            else if (existing.State != s.State)
            {
                existing.State = s.State;
                if (s.Model != null) existing.Model = s.Model;
                changed.Add(existing);
            }
        }
        foreach (var d in current.Where(d => !scanSerials.Contains(d.Serial) && d.State != DeviceState.Offline))
        {
            d.State = DeviceState.Offline;
            changed.Add(d);
        }
        return changed;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var scan = await _adb.ListDevicesAsync(ct);
        foreach (var c in Merge(_devices, scan))
            DeviceChanged?.Invoke(c);
    }

    /// 后台轮询;由 App 在 UI 线程外调用,事件回调需 marshal 回 UI。
    public async Task PollLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RefreshAsync(ct); } catch { /* 单次失败忽略,下轮重试 */ }
            try { await Task.Delay(interval, ct); } catch (TaskCanceledException) { break; }
        }
    }
}
