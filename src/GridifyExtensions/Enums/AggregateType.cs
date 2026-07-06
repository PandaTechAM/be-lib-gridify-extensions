namespace GridifyExtensions.Enums;

/// <summary>
///     Aggregation operation applied to a selected column.
/// </summary>
public enum AggregateType
{
    /// <summary>Count of distinct values.</summary>
    UniqueCount,

    /// <summary>Sum of values.</summary>
    Sum,

    /// <summary>Arithmetic mean of values.</summary>
    Average,

    /// <summary>Minimum value.</summary>
    Min,

    /// <summary>Maximum value.</summary>
    Max
}
