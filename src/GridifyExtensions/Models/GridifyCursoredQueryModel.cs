using Gridify;
using GridifyExtensions.Exceptions;

namespace GridifyExtensions.Models;

public class GridifyCursoredQueryModel
{
   private int _pageSize = 20;
   public required int PageSize
   {
      get => _pageSize;
      set
      {
         value = value switch
         {
            <= 0 => throw new GridifyException($"{nameof(PageSize)} should be positive number."),
            > 500 => 500,
            _ => value
         };

         _pageSize = value;
      }
   }
   
   public string? Filter { get; set; }
   
   internal GridifyQueryModel ToGridifyQueryModel()
   {
      return new GridifyQueryModel
      {
         Page = 1,
         PageSize = PageSize,
         OrderBy = "",
         Filter = Filter
      };
   }
}