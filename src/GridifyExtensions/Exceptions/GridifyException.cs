namespace GridifyExtensions.Exceptions;

/// <summary>
///     Thrown when filtering, ordering, pagination, or query-model validation fails.
/// </summary>
public class GridifyException(string message) : Exception(message);
