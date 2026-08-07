using FluentAssertions;
using RelicLauncher.Core.Security;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class ServerDisplaySanitizerTests
{
    [Fact]
    public void SanitizeDescription_StripsTagsAndCapsLength()
    {
        var input = "<br>Hello <a href=\"http://evil\">world</a>" + new string('x', 3000);
        var sanitized = ServerDisplaySanitizer.SanitizeDescription(input);
        sanitized.Should().NotContain("<");
        sanitized.Should().NotContain(">");
        sanitized.Length.Should().BeLessThanOrEqualTo(ServerDisplaySanitizer.MaxDescriptionLength);
    }

    [Fact]
    public void SanitizeName_RemovesControlCharacters()
    {
        var sanitized = ServerDisplaySanitizer.SanitizeName("Test\u0000\u202EServer");
        sanitized.Should().Be("TestServer");
    }
}
