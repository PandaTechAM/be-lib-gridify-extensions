using GridifyExtensions.Enums;

namespace GridifyExtensions.Models;

/// <summary>
///     Request to aggregate a single mapped property over a filtered set.
/// </summary>
public class AggregateQueryModel
{
    /// <summary>
    ///     Gridify filter expression applied before aggregation.
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    ///     Public field name of the property to aggregate.
    /// </summary>
    public required string PropertyName { get; set; }

    /// <summary>
    ///     Aggregation operation to perform.
    /// </summary>
    public required AggregateType AggregateType { get; set; }

    internal GridifyQueryModel ToGridifyQueryModel()
    {
        return new GridifyQueryModel
        {
            Page = 1,
            PageSize = 1,
            OrderBy = null,
            Filter = Filter
        };
    }
}
