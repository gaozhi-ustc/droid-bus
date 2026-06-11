using System.Globalization;

namespace DroidBus.Core.Adb;

/// 每个方法返回传给 adb.exe 的参数数组(不含 adb 本身)。
public static class AdbCommands
{
    private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

    public static string[] InstallApk(string s, string apkPath) => new[] { "-s", s, "install", "-r", apkPath };
    public static string[] Uninstall(string s, string pkg) => new[] { "-s", s, "uninstall", pkg };
    public static string[] Push(string s, string local, string remote) => new[] { "-s", s, "push", local, remote };
    public static string[] Pull(string s, string remote, string local) => new[] { "-s", s, "pull", remote, local };

    public static string[] StartApp(string s, string pkg) => new[]
    {
        "-s", s, "shell", "monkey", "-p", pkg, "-c", "android.intent.category.LAUNCHER", "1"
    };

    public static string[] Tap(string s, int x, int y) => new[] { "-s", s, "shell", "input", "tap", I(x), I(y) };
    public static string[] Swipe(string s, int x1, int y1, int x2, int y2, int ms) =>
        new[] { "-s", s, "shell", "input", "swipe", I(x1), I(y1), I(x2), I(y2), I(ms) };
    public static string[] KeyEvent(string s, int keycode) => new[] { "-s", s, "shell", "input", "keyevent", I(keycode) };
    public static string[] Text(string s, string text) => new[] { "-s", s, "shell", "input", "text", text };
    public static string[] SetShowTouches(string s, bool on) =>
        new[] { "-s", s, "shell", "settings", "put", "system", "show_touches", on ? "1" : "0" };

    public static IReadOnlyList<string> KillServer() => new[] { "kill-server" };
    public static IReadOnlyList<string> StartServer() => new[] { "start-server" };
}
