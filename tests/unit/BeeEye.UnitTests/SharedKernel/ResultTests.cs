using BeeEye.Shared.Results;
using Xunit;

namespace BeeEye.UnitTests.SharedKernel;

public sealed class ResultTests
{
    [Fact]
    public void Success_IsSuccess_AndCarriesValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_ExposesError_AndThrowsOnValueAccess()
    {
        var result = Result.Failure<int>(Error.NotFound("missing"));

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Map_OnSuccess_ProjectsValue()
    {
        var mapped = Result.Success(10).Map(x => x * 2);

        Assert.True(mapped.IsSuccess);
        Assert.Equal(20, mapped.Value);
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        var mapped = Result.Failure<int>(Error.Validation("bad")).Map(x => x * 2);

        Assert.True(mapped.IsFailure);
        Assert.Equal("validation", mapped.Error.Code);
    }

    [Fact]
    public void SuccessResult_WithError_IsRejected()
        => Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
}
