namespace GridifyExtensions.Models;

public record CursoredResponse<T>(List<T> Data, int PageSize);