using GridifyExtensions.Enums;

namespace GridifyExtensions.Models;

public class AggregateQueryModel : ColumnDistinctValueQueryModel
{
    public required AggregateType AggregateType { get; set; }
}