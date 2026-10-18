using GarageFlow.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Common;

/// <summary>Which page the caller wants. Page numbers start at 1.</summary>
public record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    /// <summary>
    /// Bad values are rejected (HTTP 400) instead of silently "fixed": a client asking for page 0 or a
    /// 10,000-row page has a bug, and hiding it would only move the problem.
    /// </summary>
    public PageRequest Validated()
    {
        if (Page < 1)
            throw new DomainException("page must be 1 or more.");
        if (PageSize < 1 || PageSize > MaxPageSize)
            throw new DomainException($"pageSize must be between 1 and {MaxPageSize}.");
        return this;
    }

    public int Skip => (Page - 1) * PageSize;
}

/// <summary>One page of results plus what the client needs to ask for the next one.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public static class PagingExtensions
{
    /// <summary>
    /// Counts the rows, then reads one page. The query MUST already have a stable ORDER BY that ends in a unique column
    /// (for example date, then Id), otherwise rows with equal sort values can appear on two pages or on none.
    /// </summary>
    public static async Task<PagedResult<TResult>> ToPagedAsync<TEntity, TResult>(
        this IQueryable<TEntity> orderedQuery,
        PageRequest? request,
        Func<TEntity, TResult> map,
        CancellationToken ct)
    {
        var page = (request ?? new PageRequest()).Validated();

        var total = await orderedQuery.CountAsync(ct);
        var rows = await orderedQuery.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);

        return new PagedResult<TResult>(rows.Select(map).ToList(), page.Page, page.PageSize, total);
    }
}
