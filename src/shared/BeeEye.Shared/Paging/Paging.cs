namespace BeeEye.Shared.Paging;

/// <summary>A validated, bounded page request. Prevents unbounded result sets.</summary>
public sealed record PageRequest
{
    public const int MaxPageSize = 500;
    public const int DefaultPageSize = 50;

    public PageRequest(int page = 1, int pageSize = DefaultPageSize)
    {
        Page = page < 1 ? 1 : page;
        PageSize = pageSize switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize,
        };
    }

    public int Page { get; }
    public int PageSize { get; }

    /// <summary>Zero-based row offset for the current page.</summary>
    public int Offset => (Page - 1) * PageSize;
}

/// <summary>A page of results plus the paging metadata a client needs to navigate.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;

    public static PagedResult<T> Empty(PageRequest request)
        => new([], request.Page, request.PageSize, 0);
}
