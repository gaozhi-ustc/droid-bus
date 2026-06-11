namespace DroidBus.Core;

public sealed class BinaryLocator
{
    public const string DefaultDir = @"C:\Program Files (x86)\Androidscreen\Resources";

    private BinaryLocator(string dir)
    {
        Adb = Require(dir, "adb.exe");
        Scrcpy = Require(dir, "scrcpy.exe");
        ScrcpyServer = Require(dir, "scrcpy-server");
        SndcpyApk = RequireWithParentFallback(dir, "sndcpy.apk");
        AdbKeyboardApk = Require(dir, "Adbkeyboard.apk");
        Dir = dir;
    }

    public string Dir { get; }
    public string Adb { get; }
    public string Scrcpy { get; }
    public string ScrcpyServer { get; }
    public string SndcpyApk { get; }
    public string AdbKeyboardApk { get; }

    public static BinaryLocator FromDirectory(string dir) => new(dir);

    public static BinaryLocator Discover()
    {
        var env = Environment.GetEnvironmentVariable("DROIDBUS_TOOLS");
        return new(string.IsNullOrWhiteSpace(env) ? DefaultDir : env);
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
