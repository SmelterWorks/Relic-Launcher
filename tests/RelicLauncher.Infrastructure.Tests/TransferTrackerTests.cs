using FluentAssertions;
using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Transfers;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class TransferTrackerTests
{
    [Fact]
    public async Task Begin_Start_Complete_UpdatesJobState()
    {
        var tracker = new TransferTracker();
        var changes = 0;
        tracker.Changed += (_, _) => changes++;

        await using var session = tracker.Begin("job-1", "Download mod", TransferJobKind.Mod);
        await session.StartAsync().ConfigureAwait(true);
        session.Report(0.5);
        session.Complete("Finished");

        var jobs = tracker.GetJobs();
        jobs.Should().ContainSingle();
        jobs[0].State.Should().Be(TransferJobState.Completed);
        jobs[0].Progress.Should().Be(1);
        jobs[0].StatusText.Should().Be("Finished");
        changes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Fail_And_Cancel_SetTerminalStates()
    {
        var tracker = new TransferTracker();

        var failed = tracker.Begin("fail", "Broken", TransferJobKind.Version);
        try
        {
            await failed.StartAsync().ConfigureAwait(true);
            failed.Fail("network");
        }
        finally
        {
            await failed.DisposeAsync().ConfigureAwait(true);
        }

        var canceled = tracker.Begin("cancel", "Stopped", TransferJobKind.Mod);
        try
        {
            await canceled.StartAsync().ConfigureAwait(true);
            canceled.Cancel();
        }
        finally
        {
            await canceled.DisposeAsync().ConfigureAwait(true);
        }

        tracker.GetJobs().Should().Contain(j => string.Equals(j.Id, "fail", StringComparison.Ordinal) && j.State == TransferJobState.Failed && j.Error == "network");
        tracker.GetJobs().Should().Contain(j => string.Equals(j.Id, "cancel", StringComparison.Ordinal) && j.State == TransferJobState.Canceled);
    }

    [Fact]
    public async Task Begin_ReplacesFinishedJob_WithSameId()
    {
        var tracker = new TransferTracker();

        var first = tracker.Begin("repeat", "First", TransferJobKind.Mod);
        try
        {
            await first.StartAsync().ConfigureAwait(true);
            first.Complete();
        }
        finally
        {
            await first.DisposeAsync().ConfigureAwait(true);
        }

        var second = tracker.Begin("repeat", "Second", TransferJobKind.Mod);
        try
        {
            await second.StartAsync().ConfigureAwait(true);
            second.Complete();
        }
        finally
        {
            await second.DisposeAsync().ConfigureAwait(true);
        }

        tracker.GetJobs().Should().ContainSingle(j => string.Equals(j.Id, "repeat", StringComparison.Ordinal) && j.Label == "Second");
    }

    [Fact]
    public async Task StartAsync_RespectsMaxConcurrent()
    {
        var tracker = new TransferTracker(maxConcurrent: 1);

        await using var first = tracker.Begin("one", "First", TransferJobKind.Version);
        await using var second = tracker.Begin("two", "Second", TransferJobKind.Version);

        var firstStarted = first.StartAsync();
        await WaitForStateAsync(tracker, "one", TransferJobState.Running).ConfigureAwait(true);

        var secondStarted = second.StartAsync();
        await Task.Delay(75).ConfigureAwait(true);
        tracker.GetJobs().Single(j => string.Equals(j.Id, "two", StringComparison.Ordinal)).State.Should().Be(TransferJobState.Queued);

        first.Complete();
        await secondStarted.ConfigureAwait(true);
        await firstStarted.ConfigureAwait(true);
        tracker.GetJobs().Single(j => string.Equals(j.Id, "two", StringComparison.Ordinal)).State.Should().Be(TransferJobState.Running);

        second.Complete();
    }

    [Fact]
    public async Task DisposeAsync_CancelsQueuedOrRunningSession()
    {
        var tracker = new TransferTracker();
        var session = tracker.Begin("dispose", "Cleanup", TransferJobKind.Mod);
        await session.DisposeAsync().ConfigureAwait(true);

        tracker.GetJobs().Should().ContainSingle(j =>
            string.Equals(j.Id, "dispose", StringComparison.Ordinal) && j.State == TransferJobState.Canceled);
    }

    [Fact]
    public async Task Trim_RemovesOldFinishedJobs()
    {
        var tracker = new TransferTracker();
        for (var i = 0; i < 25; i++)
        {
            await using var session = tracker.Begin($"job-{i}", $"Job {i}", TransferJobKind.Mod);
            await session.StartAsync().ConfigureAwait(true);
            session.Complete();
        }

        tracker.GetJobs().Count(j => j.State == TransferJobState.Completed).Should().BeLessThanOrEqualTo(20);
    }

    private static async Task WaitForStateAsync(TransferTracker tracker, string id, TransferJobState state)
    {
        for (var i = 0; i < 50; i++)
        {
            if (tracker.GetJobs().Any(j => string.Equals(j.Id, id, StringComparison.Ordinal) && j.State == state))
            {
                return;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }

        tracker.GetJobs().Should().Contain(j => string.Equals(j.Id, id, StringComparison.Ordinal) && j.State == state);
    }
}
