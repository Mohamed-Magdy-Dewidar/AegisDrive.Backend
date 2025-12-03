using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Shared.Pagination;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query , int page,int pageSize,CancellationToken cancellationToken = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : pageSize;

        // 1. Get Total Count (Database Call #1)
        var totalCount = await query.CountAsync(cancellationToken);

        // 2. Get Data Page (Database Call #2)
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, page, pageSize);
    }
}
