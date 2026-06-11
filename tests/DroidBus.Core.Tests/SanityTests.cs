using FluentAssertions;

namespace DroidBus.Core.Tests;

public class SanityTests
{
    [Fact]
    public void Sln_builds_and_tests_run()
    {
        true.Should().BeTrue();
    }
}
