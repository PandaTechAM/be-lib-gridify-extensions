using GridifyExtensions.Enums;

namespace GridifyExtensions.Models;
public class AggregateModel : GridifyQueryModel
{
    public required string PropertyName { get; set; }
    public required AggregateType AggregateType { get; set; }
}
