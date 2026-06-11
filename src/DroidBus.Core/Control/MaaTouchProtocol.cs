using System.Globalization;

namespace DroidBus.Core.Control;

/// MaaTouch / minitouch 握手横幅解析结果。
/// 注意 MaxX/MaxY 是触摸设备的「原始分辨率」,通常 ≠ 显示分辨率(1440×2960),
/// 注入坐标必须按它换算(见 <see cref="PointerStreamMapper.ToTarget"/>)。
public readonly record struct MaaTouchInfo(
    int MaxContacts, int MaxX, int MaxY, int MaxPressure, string Version, int Pid);

/// MaaTouch(Android 上 minitouch 协议的 app_process+InputManager 实现)的纯协议层:
/// 解析启动横幅 `v/^/$`,构造 `d/m/u/c/r/w` 命令行。无 IO、无进程,全可单测。
///
/// 命令均为单行(不含换行),由通道负责按 \n 分隔写入;一次 down/move/up 后需 `c`(commit)
/// 才会真正注入。坐标单位是握手 `^` 行给出的 MaxX/MaxY,压力是 0..MaxPressure。
public static class MaaTouchProtocol
{
    /// 扫描横幅各行,取 `^ <maxContacts> <maxX> <maxY> <maxPressure>`、`v <ver>`、`$ <pid>`。
    /// 容忍乱序与噪声行;缺失字段取默认值。
    public static MaaTouchInfo ParseHandshake(IEnumerable<string> lines)
    {
        int contacts = 0, maxX = 0, maxY = 0, maxP = 0, pid = 0;
        var version = "";

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0])
            {
                case "^" when parts.Length >= 5:
                    contacts = ParseInt(parts[1]);
                    maxX = ParseInt(parts[2]);
                    maxY = ParseInt(parts[3]);
                    maxP = ParseInt(parts[4]);
                    break;
                case "v" when parts.Length >= 2:
                    version = parts[1];
                    break;
                case "$" when parts.Length >= 2:
                    pid = ParseInt(parts[1]);
                    break;
            }
        }
        return new MaaTouchInfo(contacts, maxX, maxY, maxP, version, pid);
    }

    public static string Down(int contactId, int x, int y, int pressure)
        => $"d {contactId} {x} {y} {pressure}";

    public static string Move(int contactId, int x, int y, int pressure)
        => $"m {contactId} {x} {y} {pressure}";

    public static string Up(int contactId) => $"u {contactId}";
    public static string Commit() => "c";
    public static string Reset() => "r";
    public static string Wait(int milliseconds) => $"w {milliseconds}";

    /// 归一化压力 0..1 → 设备 [0,maxPressure],四舍五入并钳制。
    public static int ToPressure(float normalized, int maxPressure)
    {
        if (maxPressure <= 0) return 0;
        var clamped = normalized < 0f ? 0f : normalized > 1f ? 1f : normalized;
        var v = (int)System.Math.Round(clamped * maxPressure, System.MidpointRounding.AwayFromZero);
        return v < 0 ? 0 : v > maxPressure ? maxPressure : v;
    }

    private static int ParseInt(string s)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
