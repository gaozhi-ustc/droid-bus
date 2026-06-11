using DroidBus.Core.Control;
using FluentAssertions;

namespace DroidBus.Core.Tests;

public class ShellArgsTests
{
    [Fact]
    public void Splits_on_whitespace()
    {
        ShellArgs.Split("input keyevent 24").Should().Equal("input", "keyevent", "24");
    }

    [Fact]
    public void Collapses_runs_of_whitespace_including_tabs()
    {
        ShellArgs.Split("a  \t b").Should().Equal("a", "b");
    }

    [Fact]
    public void Keeps_double_quoted_words_together_and_strips_quotes()
    {
        ShellArgs.Split("am start -e key \"hello world\"")
            .Should().Equal("am", "start", "-e", "key", "hello world");
    }

    [Fact]
    public void Keeps_single_quoted_words_together_and_strips_quotes()
    {
        ShellArgs.Split("echo 'a b c'").Should().Equal("echo", "a b c");
    }

    [Fact]
    public void Concatenates_quoted_and_unquoted_segments_in_one_token()
    {
        ShellArgs.Split("key=\"hello world\"").Should().Equal("key=hello world");
    }

    [Fact]
    public void Empty_quotes_produce_an_explicit_empty_argument()
    {
        ShellArgs.Split("set \"\"").Should().Equal("set", "");
    }

    [Fact]
    public void Unterminated_quote_takes_the_rest_of_the_line()
    {
        ShellArgs.Split("echo \"abc").Should().Equal("echo", "abc");
    }

    [Fact]
    public void Empty_input_yields_no_tokens()
    {
        ShellArgs.Split("   ").Should().BeEmpty();
    }
}
