using FluentAssertions;
using RelicLauncher.Core.Results;
using Xunit;

namespace RelicLauncher.Core.Tests;

public class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();
        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Failure_RejectsBlankMessage(string error)
    {
        var act = () => Result.Failure(error);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Failure_PreservesMessage()
    {
        var result = Result.Failure("launch failed");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("launch failed");
    }

    [Fact]
    public void GenericSuccess_KeepsValue()
    {
        var result = Result<int>.Success(7);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(7);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void GenericFailure_ClearsValue()
    {
        var result = Result<string>.Failure("missing");
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().Be("missing");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GenericFailure_RejectsBlankMessage(string? error)
    {
        var act = () => Result<string>.Failure(error!);
        act.Should().Throw<ArgumentException>();
    }
}
