namespace GridifyExtensions.Models;

/// <summary>
///     Cursor-based query for the distinct values of a single mapped column.
/// </summary>
public class ColumnDistinctValueCursoredQueryModel : GridifyCursoredQueryModel
{
    /// <summary>
    ///     Public field name of the column whose distinct values are requested.
    /// </summary>
    public required string PropertyName { get; set; }
}
