using System.Runtime.InteropServices;

namespace DroidBus.Core;

/// 定位 adb / scrcpy / scrcpy-server 与 apk。
/// 跨平台:按运行时 RID(win-x64 / linux-x64 / osx-x64 / osx-arm64)在 tools/&lt;rid&gt; 下查找,
/// 找不到回退系统 PATH;Windows 另回退原「安卓投屏」Resources 目录。
public sealed class BinaryLocator
{
    /// 原「安卓投屏」捆绑目录(仅 Windows 作为回退候选)。
    public const string DefaultWindowsDir = @"C:\Program Files (x86)\Androidscreen\Resources";

    private BinaryLocator(
        string dir, string adb, string scrcpy, string scrcpyServer, string sndcpyApk, string adbKeyboardApk)
    {
        Dir = dir;
        Adb = adb;
        Scrcpy = scrcpy;
        ScrcpyServer = scrcpyServer;
        SndcpyApk = sndcpyApk;
        AdbKeyboardApk = adbKeyboardApk;
    }

    /// 主工具所在目录(adb 的目录),供 scrcpy 进程的 WorkingDirectory 使用。
    public string Dir { get; }
    public string Adb { get; }
    public string Scrcpy { get; }
    public string ScrcpyServer { get; }
    /// apk 为可选能力(音频/中文输入);缺失时为「期望路径」,实际调用 adb install 时才会失败并降级。
    public string SndcpyApk { get; }
    public string AdbKeyboardApk { get; }

    /// 当前运行时 RID,归一为四个目标平台之一。
    public static string Rid { get; } = DetectRid();

    private static string DetectRid()
    {
        if (OperatingSystem.IsWindows()) return "win-x64";
        if (OperatingSystem.IsLinux()) return "linux-x64";
        if (OperatingSystem.IsMacOS())
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return RuntimeInformation.RuntimeIdentifier;
    }

    /// 平台可执行文件名(Windows 加 .exe)。
    public static string ExeName(string baseName) =>
        OperatingSystem.IsWindows() ? baseName + ".exe" : baseName;

    /// 从一个明确目录定位,要求齐全(沿用原行为;主要给测试与 Windows 捆绑目录)。
    public static BinaryLocator FromDirectory(string dir)
    {
        var adb = Require(dir, ExeName("adb"));
        var scrcpy = Require(dir, ExeName("scrcpy"));
        var server = Require(dir, "scrcpy-server");
        var sndcpy = RequireWithParentFallback(dir, "sndcpy.apk");
        var kbd = Require(dir, "Adbkeyboard.apk");
        return new BinaryLocator(dir, adb, scrcpy, server, sndcpy, kbd);
    }

    /// 跨平台自动发现。解析顺序:环境变量 DROIDBUS_TOOLS → tools/&lt;rid&gt;/ → 系统 PATH →(Windows)默认 Resources 目录。
    public static BinaryLocator Discover()
    {
        var toolDirs = ToolSearchDirs();

        var adb = ResolveExecutable("adb", toolDirs)
            ?? throw new FileNotFoundException(
                $"找不到 adb。请安装(Ubuntu: `sudo apt install adb`)或放入 tools/{Rid}/,或设 DROIDBUS_TOOLS。");
        var scrcpy = ResolveExecutable("scrcpy", toolDirs)
            ?? throw new FileNotFoundException(
                $"找不到 scrcpy。请安装(Ubuntu: `sudo apt install scrcpy`)或放入 tools/{Rid}/,或设 DROIDBUS_TOOLS。");
        var server = ResolveScrcpyServer(toolDirs)
            ?? throw new FileNotFoundException(
                "找不到 scrcpy-server。它随 scrcpy 提供(如 /usr/share/scrcpy/scrcpy-server);" +
                $"或放入 tools/{Rid}/,或设 SCRCPY_SERVER_PATH。");

        // apk 可选:解析到则用真实路径,否则给出期望路径(调用时才暴露缺失)。
        var sndcpy = ResolveOptional("sndcpy.apk", toolDirs) ?? ExpectedToolPath("sndcpy.apk", toolDirs);
        var kbd = ResolveOptional("Adbkeyboard.apk", toolDirs) ?? ExpectedToolPath("Adbkeyboard.apk", toolDirs);

        return new BinaryLocator(Path.GetDirectoryName(adb) ?? ".", adb, scrcpy, server, sndcpy, kbd);
    }

    /// 候选工具目录(按优先级):DROIDBUS_TOOLS → 输出目录及其上层的 tools/&lt;rid&gt; →(Windows)默认 Resources。
    private static IReadOnlyList<string> ToolSearchDirs()
    {
        var dirs = new List<string>();

        var env = Environment.GetEnvironmentVariable("DROIDBUS_TOOLS");
        if (!string.IsNullOrWhiteSpace(env)) dirs.Add(env);

        // 从程序输出目录向上若干层找 tools/<rid>(兼容 dotnet run 时 bin/.../net8.0 与已发布布局)。
        var probe = AppContext.BaseDirectory;
        for (var i = 0; i < 6 && probe is not null; i++)
        {
            dirs.Add(Path.Combine(probe, "tools", Rid));
            probe = Path.GetDirectoryName(probe.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (OperatingSystem.IsWindows()) dirs.Add(DefaultWindowsDir);
        return dirs;
    }

    private static string? ResolveExecutable(string baseName, IReadOnlyList<string> toolDirs)
    {
        var file = ExeName(baseName);
        foreach (var d in toolDirs)
        {
            var p = Path.Combine(d, file);
            if (File.Exists(p)) return p;
        }
        return FindOnPath(file);
    }

    private static string? ResolveScrcpyServer(IReadOnlyList<string> toolDirs)
    {
        var explicitPath = Environment.GetEnvironmentVariable("SCRCPY_SERVER_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return explicitPath;

        foreach (var d in toolDirs)
        {
            var p = Path.Combine(d, "scrcpy-server");
            if (File.Exists(p)) return p;
        }
        // scrcpy 包通常把 server 放在共享目录。
        foreach (var d in new[]
                 {
                     "/usr/share/scrcpy", "/usr/local/share/scrcpy",
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/scrcpy"),
                     "/opt/homebrew/share/scrcpy", "/usr/local/Cellar/scrcpy",
                 })
        {
            var p = Path.Combine(d, "scrcpy-server");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static string? ResolveOptional(string file, IReadOnlyList<string> toolDirs)
    {
        foreach (var d in toolDirs)
        {
            var p = Path.Combine(d, file);
            if (File.Exists(p)) return p;
            // sndcpy.apk 在原 Windows 包里位于 Resources 父目录。
            var parent = Path.GetDirectoryName(d.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (parent is not null)
            {
                var pp = Path.Combine(parent, file);
                if (File.Exists(pp)) return pp;
            }
        }
        return null;
    }

    private static string ExpectedToolPath(string file, IReadOnlyList<string> toolDirs) =>
        Path.Combine(toolDirs.Count > 0 ? toolDirs[0] : ".", file);

    /// 在 PATH 中查找可执行文件,返回绝对路径或 null。
    private static string? FindOnPath(string file)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate;
            try { candidate = Path.Combine(dir.Trim(), file); }
            catch { continue; }
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string Require(string dir, string file)
    {
        var p = Path.Combine(dir, file);
        if (!File.Exists(p))
            throw new FileNotFoundException($"找不到必需的二进制 {file}(目录:{dir})", p);
        return p;
    }

    // sndcpy.apk 在原 app 中位于 Resources 的父目录(Androidscreen\),不在 Resources\ 内。
    private static string RequireWithParentFallback(string dir, string file)
    {
        var p = Path.Combine(dir, file);
        if (File.Exists(p)) return p;
        var parent = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (parent is not null)
        {
            var pp = Path.Combine(parent, file);
            if (File.Exists(pp)) return pp;
        }
        throw new FileNotFoundException($"找不到必需的二进制 {file}(目录:{dir} 或其父目录)", p);
    }
}
