using GridifyExtensions.Enums;

namespace GridifyExtensions.Models;

public class AggregateQueryModel
{
   public string? Filter { get; set; }
   public required string PropertyName { get; set; }
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
