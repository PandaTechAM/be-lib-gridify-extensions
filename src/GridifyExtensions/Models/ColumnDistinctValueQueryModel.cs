namespace GridifyExtensions.Models;

/// <summary>
///     Query for the distinct values of a single mapped column. Uses the standard Page/PageSize offset
///     pagination provided by <see cref="GridifyQueryModel" />.
/// </summary>
public class ColumnDistinctValueQueryModel : GridifyQueryModel
{
    /// <summary>
    ///     Public field name of the column whose distinct values are requested.
    /// </summary>
    public required string PropertyName { get; set; }
}
