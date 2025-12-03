namespace AegisDrive.Api.Shared.Pagination;

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public PaginationMetadata Pagination { get; set; }

    public PagedResult(IEnumerable<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        Pagination = new PaginationMetadata
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
}

public class PaginationMetadata
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}