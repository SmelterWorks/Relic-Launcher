using FluentAssertions;
using RelicLauncher.App.Services;
using RelicLauncher.App.ViewModels;
using Xunit;

namespace RelicLauncher.App.Tests;

public class ToastServiceTests
{
    [Fact]
    public void Show_RespectsMaxVisibleCount()
    {
        var host = new ToastHostViewModel();
        var service = new ToastService(host);

        service.Show(new ToastRequest { Message = "one" });
        service.Show(new ToastRequest { Message = "two" });
        service.Show(new ToastRequest { Message = "three" });
        service.Show(new ToastRequest { Message = "four" });

        host.Items.Should().HaveCount(3);
        host.Items[^1].Message.Should().Be("four");
    }
}
