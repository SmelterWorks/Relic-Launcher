using FluentAssertions;
using RelicLauncher.App.Services;
using Xunit;

namespace RelicLauncher.App.Tests;

public class ConfirmDialogServiceTests
{
    [Fact]
    public async Task ConfirmAsync_WithoutMainWindow_ReturnsFalse()
    {
        var service = new AvaloniaConfirmDialogService(new MainWindowHolder());
        var confirmed = await service.ConfirmAsync("Title", "Message");
        confirmed.Should().BeFalse();
    }
}
