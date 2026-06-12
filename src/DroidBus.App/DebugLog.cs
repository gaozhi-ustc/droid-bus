namespace DroidBus.App;

/// 轻量文件日志(诊断嵌入/启动用)。设 DROIDBUS_DEBUG 或始终写到 /tmp。
public static class DebugLog
{
    private static readonly object Lock = new();
    private static readonly string Path =
        Environment.GetEnvironmentVariable("DROIDBUS_LOG")
        ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "droidbus-debug.log");

    public static void Write(string msg)
    {
        try
        {
            lock (Lock)
                File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
        }
        catch { /* 日志失败不影响主流程 */ }
    }
}
