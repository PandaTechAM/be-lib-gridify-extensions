namespace GridifyExtensions.Models;

/// <summary>
///     A page of results with paging metadata and the total row count.
/// </summary>
/// <param name="Data">Items on the current page.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Requested page size.</param>
/// <param name="TotalCount">Total number of rows matching the filter.</param>
public record PagedResponse<T>(List<T> Data, int Page, int PageSize, long TotalCount);
