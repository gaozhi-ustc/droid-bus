namespace DroidBus.Core.Audio;

/// 构造 sndcpy 音频转发所需的 adb 参数(Android 10 用)。
public static class SndcpyCommands
{
    public static IReadOnlyList<string> Install(string serial, string apkPath) =>
        new[] { "-s", serial, "install", "-r", apkPath };

    public static IReadOnlyList<string> Forward(string serial, int port) =>
        new[] { "-s", serial, "forward", $"tcp:{port}", "localabstract:sndcpy" };

    public static IReadOnlyList<string> StartService(string serial) =>
        new[] { "-s", serial, "shell", "am", "start-foreground-service",
            "com.rom1v.sndcpy/.RecordService" };
}
