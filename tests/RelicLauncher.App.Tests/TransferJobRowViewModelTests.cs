using FluentAssertions;
using RelicLauncher.App.ViewModels;
using RelicLauncher.Core.Models;
using Xunit;

namespace RelicLauncher.App.Tests;

public class TransferJobRowViewModelTests
{
    [Fact]
    public void Constructor_MapsActiveModJob()
    {
        var job = new TransferJob
        {
            Id = "mod-1",
            Label = "Mod Example",
            Kind = TransferJobKind.Mod,
            State = TransferJobState.Running,
            Progress = 0.42,
            StatusText = "Downloading",
        };

        var row = new TransferJobRowViewModel(job);

        row.Id.Should().Be("mod-1");
        row.Label.Should().Be("Mod Example");
        row.Kind.Should().Be("Mod");
        row.IsActive.Should().BeTrue();
        row.Progress.Should().Be(0.42);
        row.StatusText.Should().Be("Downloading");
    }

    [Fact]
    public void Constructor_MapsCompletedModpackJobAsInactive()
    {
        var job = new TransferJob
        {
            Id = "modpack-1",
            Label = "Modpack Pack",
            Kind = TransferJobKind.Modpack,
            State = TransferJobState.Completed,
            Progress = 1,
            StatusText = "Applied Pack",
        };

        var row = new TransferJobRowViewModel(job);

        row.Kind.Should().Be("Modpack");
        row.IsActive.Should().BeFalse();
        row.StatusText.Should().Be("Applied Pack");
    }
}
