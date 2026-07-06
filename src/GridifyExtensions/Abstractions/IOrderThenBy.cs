namespace GridifyExtensions.Abstractions;

/// <summary>
///     Fluent builder for chaining default order-by clauses on a mapper.
/// </summary>
public interface IOrderThenBy
{
    /// <summary>
    ///     Append an ascending order-by clause for the given column.
    /// </summary>
    IOrderThenBy ThenBy(string column);

    /// <summary>
    ///     Append a descending order-by clause for the given column.
    /// </summary>
    IOrderThenBy ThenByDescending(string column);
}
