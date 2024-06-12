using GridifyExtensions.Enums;

namespace GridifyExtensions.Models;
public class AggregateQueryModel : GridifyQueryModel
{
    public required string PropertyName { get; set; }
    public required AggregateType AggregateType { get; set; }
}
