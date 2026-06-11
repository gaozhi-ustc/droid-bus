using DroidBus.Core;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class BinaryLocatorTests
{
    [Fact]
    public void Locates_tools_in_given_dir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dbtools_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var f in new[] { "adb.exe", "scrcpy.exe", "scrcpy-server", "sndcpy.apk", "Adbkeyboard.apk" })
            File.WriteAllText(Path.Combine(dir, f), "x");

        var locator = BinaryLocator.FromDirectory(dir);

        locator.Adb.Should().Be(Path.Combine(dir, "adb.exe"));
        locator.Scrcpy.Should().Be(Path.Combine(dir, "scrcpy.exe"));
        locator.SndcpyApk.Should().Be(Path.Combine(dir, "sndcpy.apk"));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Throws_when_adb_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dbtools_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var act = () => BinaryLocator.FromDirectory(dir);
        act.Should().Throw<FileNotFoundException>().WithMessage("*adb.exe*");
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Finds_sndcpy_apk_in_parent_dir()
    {
        var parent = Path.Combine(Path.GetTempPath(), "dbtools_" + Guid.NewGuid().ToString("N"));
        var res = Path.Combine(parent, "Resources");
        Directory.CreateDirectory(res);
        foreach (var f in new[] { "adb.exe", "scrcpy.exe", "scrcpy-server", "Adbkeyboard.apk" })
            File.WriteAllText(Path.Combine(res, f), "x");
        File.WriteAllText(Path.Combine(parent, "sndcpy.apk"), "x"); // 仅父目录有

        var locator = BinaryLocator.FromDirectory(res);

        locator.SndcpyApk.Should().Be(Path.Combine(parent, "sndcpy.apk"));
        Directory.Delete(parent, true);
    }
}
