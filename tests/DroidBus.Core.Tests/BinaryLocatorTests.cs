using DroidBus.Core;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class BinaryLocatorTests
{
    private static string Adb => BinaryLocator.ExeName("adb");
    private static string Scrcpy => BinaryLocator.ExeName("scrcpy");

    [Fact]
    public void Locates_tools_in_given_dir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dbtools_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var f in new[] { Adb, Scrcpy, "scrcpy-server", "sndcpy.apk", "Adbkeyboard.apk" })
            File.WriteAllText(Path.Combine(dir, f), "x");

        var locator = BinaryLocator.FromDirectory(dir);

        locator.Adb.Should().Be(Path.Combine(dir, Adb));
        locator.Scrcpy.Should().Be(Path.Combine(dir, Scrcpy));
        locator.SndcpyApk.Should().Be(Path.Combine(dir, "sndcpy.apk"));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Throws_when_adb_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dbtools_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var act = () => BinaryLocator.FromDirectory(dir);
        act.Should().Throw<FileNotFoundException>().WithMessage($"*{Adb}*");
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Finds_sndcpy_apk_in_parent_dir()
    {
        var parent = Path.Combine(Path.GetTempPath(), "dbtools_" + Guid.NewGuid().ToString("N"));
        var res = Path.Combine(parent, "Resources");
        Directory.CreateDirectory(res);
        foreach (var f in new[] { Adb, Scrcpy, "scrcpy-server", "Adbkeyboard.apk" })
            File.WriteAllText(Path.Combine(res, f), "x");
        File.WriteAllText(Path.Combine(parent, "sndcpy.apk"), "x"); // 仅父目录有

        var locator = BinaryLocator.FromDirectory(res);

        locator.SndcpyApk.Should().Be(Path.Combine(parent, "sndcpy.apk"));
        Directory.Delete(parent, true);
    }

    [Fact]
    public void Rid_is_one_of_the_four_targets()
    {
        BinaryLocator.Rid.Should().BeOneOf("win-x64", "linux-x64", "osx-x64", "osx-arm64");
    }

    [Fact]
    public void ExeName_appends_exe_only_on_windows()
    {
        var expected = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
        BinaryLocator.ExeName("adb").Should().Be(expected);
    }

    [Fact]
    public void Discover_resolves_tools_via_DROIDBUS_TOOLS_env()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dbtools_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var f in new[] { Adb, Scrcpy, "scrcpy-server", "sndcpy.apk", "Adbkeyboard.apk" })
            File.WriteAllText(Path.Combine(dir, f), "x");

        var prev = Environment.GetEnvironmentVariable("DROIDBUS_TOOLS");
        try
        {
            Environment.SetEnvironmentVariable("DROIDBUS_TOOLS", dir);
            var locator = BinaryLocator.Discover();
            locator.Adb.Should().Be(Path.Combine(dir, Adb));
            locator.Scrcpy.Should().Be(Path.Combine(dir, Scrcpy));
            locator.ScrcpyServer.Should().Be(Path.Combine(dir, "scrcpy-server"));
            locator.SndcpyApk.Should().Be(Path.Combine(dir, "sndcpy.apk"));
            locator.AdbKeyboardApk.Should().Be(Path.Combine(dir, "Adbkeyboard.apk"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DROIDBUS_TOOLS", prev);
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Discover_succeeds_on_dev_machine_with_tools_present()
    {
        // 在有 tools/linux-x64/ 或系统 PATH 自带 adb 的开发机上 Discover() 应成功不抛。
        var locator = BinaryLocator.Discover();
        locator.Adb.Should().NotBeNull();
        File.Exists(locator.Adb).Should().BeTrue();
    }
}
