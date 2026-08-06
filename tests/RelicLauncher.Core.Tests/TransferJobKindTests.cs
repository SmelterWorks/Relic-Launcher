using FluentAssertions;
using RelicLauncher.Core.Models;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class TransferJobKindTests
{
    [Fact]
    public void Enum_IncludesModpackKind()
    {
        Enum.GetNames<TransferJobKind>().Should().Contain("Modpack");
        TransferJobKind.Modpack.Should().BeDefined();
    }
}
