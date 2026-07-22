using BeeEye.Shared.Paging;
using Xunit;

namespace BeeEye.UnitTests.SharedKernel;

public sealed class PagingTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void PageRequest_ClampsPageToAtLeastOne(int input, int expected)
        => Assert.Equal(expected, new PageRequest(input).Page);

    [Theory]
    [InlineData(0, PageRequest.DefaultPageSize)]
    [InlineData(10_000, PageRequest.MaxPageSize)]
    [InlineData(25, 25)]
    public void PageRequest_ClampsPageSizeToBounds(int input, int expected)
        => Assert.Equal(expected, new PageRequest(1, input).PageSize);

    [Fact]
    public void PageRequest_ComputesZeroBasedOffset()
        => Assert.Equal(100, new PageRequest(3, 50).Offset);

    [Fact]
    public void PagedResult_ComputesTotalPagesAndNavigation()
    {
        var page = new PagedResult<int>([1, 2, 3], Page: 2, PageSize: 3, TotalCount: 7);

        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasNext);
        Assert.True(page.HasPrevious);
    }
}
