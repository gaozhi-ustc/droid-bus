using DroidBus.Core.Input;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ImeCommandsTests
{
    [Fact]
    public void Enable_and_set_make_adbkeyboard_default()
    {
        ImeCommands.Enable("S1").Should().Equal(
            "-s", "S1", "shell", "ime", "enable", "com.android.adbkeyboard/.AdbIME");
        ImeCommands.Set("S1").Should().Equal(
            "-s", "S1", "shell", "ime", "set", "com.android.adbkeyboard/.AdbIME");
    }

    [Fact]
    public void TypeUnicode_broadcasts_msg_extra()
    {
        ImeCommands.TypeUnicode("S1", "你好")
            .Should().Equal("-s", "S1", "shell", "am", "broadcast", "-a",
                "ADB_INPUT_TEXT", "--es", "msg", "你好");
    }
}
