using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LogisticDesk.DTOs
{
    /// <summary>Query string parameters for paged list endpoints.</summary>
    public class PaginationQuery
    {
        public const int MaxPageSize = 100;

        /// <summary>1-based page number.</summary>
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        /// <summary>Items per page (1-100).</summary>
        [Range(1, MaxPageSize)]
        public int PageSize { get; set; } = 20;
    }

    /// <summary>One page of results plus the metadata a client needs to page further.</summary>
    public class PagedResult<T>
    {
        public required IReadOnlyList<T> Items { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }

    public static class PaginationExtensions
    {
        /// <summary>
        /// Counts and fetches one page of <paramref name="source"/>. The caller must
        /// apply a deterministic ordering first, otherwise pages can overlap.
        /// </summary>
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this IOrderedQueryable<T> source, PaginationQuery query, CancellationToken cancellationToken = default)
        {
            // Clamp defensively in case model validation is bypassed (e.g. direct calls in tests).
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, PaginationQuery.MaxPageSize);

            var total = await source.CountAsync(cancellationToken);
            var items = await source
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<T>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
            };
        }
    }
}
