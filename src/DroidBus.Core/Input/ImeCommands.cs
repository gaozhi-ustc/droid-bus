namespace DroidBus.Core.Input;

/// ADBKeyBoard IME 命令:启用/设为默认/广播 Unicode 文本。
public static class ImeCommands
{
    private const string Component = "com.android.adbkeyboard/.AdbIME";

    public static IReadOnlyList<string> Enable(string serial) =>
        new[] { "-s", serial, "shell", "ime", "enable", Component };

    public static IReadOnlyList<string> Set(string serial) =>
        new[] { "-s", serial, "shell", "ime", "set", Component };

    public static IReadOnlyList<string> TypeUnicode(string serial, string text) =>
        new[] { "-s", serial, "shell", "am", "broadcast", "-a", "ADB_INPUT_TEXT", "--es", "msg", text };
}
