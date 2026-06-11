using System.Globalization;

namespace DroidBus.Core.Mirror;

public static class ScrcpyArgsBuilder
{
    /// 窗口标题:嵌入端按它精确定位 SDL 渲染窗(并发投屏时不能只靠 PID)。
    public static string WindowTitle(string serial) => $"DroidBus:{serial}";

    public static IReadOnlyList<string> Build(string serial, MirrorOptions o, DateTime? now = null)
    {
        var a = new List<string>
        {
            "-s", serial,
            "--window-borderless",
            "--window-title", WindowTitle(serial),
            "--video-bit-rate", $"{o.BitRateMbps}M",
            "--max-size", o.MaxSize.ToString(CultureInfo.InvariantCulture),
        };
        if (!string.IsNullOrWhiteSpace(o.RenderDriver))
        {
            a.Add("--render-driver");
            a.Add(o.RenderDriver);
        }
        if (o.NoAudio) a.Add("--no-audio");
        if (o.TurnScreenOff) a.Add("--turn-screen-off");
        if (o.StayAwake) a.Add("--stay-awake");
        if (o.ShowTouches) a.Add("--show-touches");
        if (o.LockOrientation is int rot)
        {
            a.Add("--lock-video-orientation");
            a.Add(rot.ToString(CultureInfo.InvariantCulture));
        }
        if (o.Record)
        {
            var ts = (now ?? DateTime.Now).ToString("yyyyMMdd-HHmmss");
            a.Add("--record");
            a.Add(Path.Combine(o.RecordDir, $"{serial}-{ts}.mp4"));
        }
        return a;
    }
}
