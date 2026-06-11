using DroidBus.Core.Adb;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class AdbCommandsTests
{
    [Fact]
    public void InstallApk_builds_args()
    {
        AdbCommands.InstallApk("S1", @"C:\a.apk")
            .Should().Equal("-s", "S1", "install", "-r", @"C:\a.apk");
    }

    [Fact]
    public void Uninstall_builds_args()
    {
        AdbCommands.Uninstall("S1", "com.x")
            .Should().Equal("-s", "S1", "uninstall", "com.x");
    }

    [Fact]
    public void Push_and_Pull_build_args()
    {
        AdbCommands.Push("S1", @"C:\f", "/sdcard/f").Should().Equal("-s", "S1", "push", @"C:\f", "/sdcard/f");
        AdbCommands.Pull("S1", "/sdcard/f", @"C:\f").Should().Equal("-s", "S1", "pull", "/sdcard/f", @"C:\f");
    }

    [Fact]
    public void StartApp_uses_monkey()
    {
        AdbCommands.StartApp("S1", "com.tencent.mobileqq")
            .Should().Equal("-s", "S1", "shell", "monkey", "-p", "com.tencent.mobileqq",
                            "-c", "android.intent.category.LAUNCHER", "1");
    }

    [Fact]
    public void Input_helpers_build_args()
    {
        AdbCommands.Tap("S1", 191, 832).Should().Equal("-s", "S1", "shell", "input", "tap", "191", "832");
        AdbCommands.Swipe("S1", 1, 2, 3, 4, 300).Should().Equal("-s", "S1", "shell", "input", "swipe", "1", "2", "3", "4", "300");
        AdbCommands.KeyEvent("S1", 3).Should().Equal("-s", "S1", "shell", "input", "keyevent", "3");
        AdbCommands.Text("S1", "abc").Should().Equal("-s", "S1", "shell", "input", "text", "abc");
        AdbCommands.SetShowTouches("S1", true).Should().Equal("-s", "S1", "shell", "settings", "put", "system", "show_touches", "1");
    }

    [Fact]
    public void KillServer_and_StartServer_args()
    {
        AdbCommands.KillServer().Should().Equal("kill-server");
        AdbCommands.StartServer().Should().Equal("start-server");
    }
}
