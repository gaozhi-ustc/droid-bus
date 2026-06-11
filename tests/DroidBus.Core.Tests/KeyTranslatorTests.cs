using DroidBus.Core.Control;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class KeyTranslatorTests
{
    [Fact]
    public void Printable_char_becomes_type_text()
    {
        KeyTranslator.FromChar('a').Should().Be(new TypeTextAction("a"));
        KeyTranslator.FromChar(' ').Should().Be(new TypeTextAction(" "));
    }

    [Fact]
    public void Composed_cjk_char_becomes_type_text()
    {
        // IME 合成后的汉字以单个字符经 KeyPress 交付,直接转文本注入。
        KeyTranslator.FromChar('中').Should().Be(new TypeTextAction("中"));
    }

    [Fact]
    public void Control_chars_are_ignored_by_FromChar()
    {
        // \r \b \t 由 FromVirtualKey 走 keyevent,FromChar 必须忽略,避免重复触发。
        KeyTranslator.FromChar('\r').Should().BeNull();
        KeyTranslator.FromChar('\b').Should().BeNull();
        KeyTranslator.FromChar('\t').Should().BeNull();
    }

    [Theory]
    [InlineData(0x0D, 66)]  // VK_RETURN  -> KEYCODE_ENTER
    [InlineData(0x08, 67)]  // VK_BACK    -> KEYCODE_DEL (退格)
    [InlineData(0x09, 61)]  // VK_TAB     -> KEYCODE_TAB
    [InlineData(0x1B, 111)] // VK_ESCAPE  -> KEYCODE_ESCAPE
    [InlineData(0x25, 21)]  // VK_LEFT    -> KEYCODE_DPAD_LEFT
    [InlineData(0x26, 19)]  // VK_UP      -> KEYCODE_DPAD_UP
    [InlineData(0x27, 22)]  // VK_RIGHT   -> KEYCODE_DPAD_RIGHT
    [InlineData(0x28, 20)]  // VK_DOWN    -> KEYCODE_DPAD_DOWN
    [InlineData(0x2E, 112)] // VK_DELETE  -> KEYCODE_FORWARD_DEL
    [InlineData(0x24, 122)] // VK_HOME    -> KEYCODE_MOVE_HOME
    [InlineData(0x23, 123)] // VK_END     -> KEYCODE_MOVE_END
    [InlineData(0x21, 92)]  // VK_PRIOR   -> KEYCODE_PAGE_UP
    [InlineData(0x22, 93)]  // VK_NEXT    -> KEYCODE_PAGE_DOWN
    public void Special_keys_map_to_android_keyevents(int vk, int expected)
    {
        KeyTranslator.FromVirtualKey(vk).Should().Be(new KeyEventAction(expected));
    }

    [Fact]
    public void Normal_letter_vk_is_not_a_special_key()
    {
        // 'A' (0x41) 是普通字符键,由 FromChar 处理,FromVirtualKey 返回 null。
        KeyTranslator.FromVirtualKey(0x41).Should().BeNull();
    }
}
