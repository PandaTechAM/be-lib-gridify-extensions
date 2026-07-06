namespace GridifyExtensions.Models;

/// <summary>
///     A page of results for cursor-based pagination.
/// </summary>
/// <param name="Data">Items on the current page.</param>
/// <param name="PageSize">Requested page size.</param>
public record CursoredResponse<T>(List<T> Data, int PageSize);
