using System.IO;
using FluentAssertions;
using RelicLauncher.Infrastructure.IO;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests;

public class BoundedStreamCopyTests
{
    [Fact]
    public async Task CopyAsync_RejectsContentLengthAboveMax()
    {
        using var input = new MemoryStream(new byte[16]);
        using var output = new MemoryStream();

        var result = await BoundedStreamCopy.CopyAsync(input, output, contentLength: 2048, maxBytes: 1024, progress: null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("maximum");
        output.Length.Should().Be(0);
    }

    [Fact]
    public async Task CopyAsync_CopiesWithinLimit()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        var progress = new RecordingProgress();

        var result = await BoundedStreamCopy.CopyAsync(
            input,
            output,
            contentLength: data.Length,
            maxBytes: 1024,
            progress,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        output.ToArray().Should().Equal(data);
        progress.Values.Should().NotBeEmpty();
        progress.Values.Last().Should().Be(1.0);
    }

    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => Values.Add(value);
    }

    [Fact]
    public async Task CopyAsync_FailsWhenStreamExceedsMax()
    {
        using var input = new MemoryStream(new byte[2048]);
        using var output = new MemoryStream();

        var result = await BoundedStreamCopy.CopyAsync(input, output, contentLength: null, maxBytes: 1024, progress: null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("exceeded");
    }
}
