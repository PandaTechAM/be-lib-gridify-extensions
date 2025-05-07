using GridifyExtensions.Exceptions;

namespace GridifyExtensions.Models;

public class GridifyCursoredQueryModel(bool validatePageSize)
{
   private int _pageSize = 20;
   
   private bool _validatePageSize = validatePageSize;

   public GridifyCursoredQueryModel() : this(true)
   {
   }

   public required int PageSize
   {
      get => _pageSize;
      set
      {
         value = value switch
         {
            <= 0 => throw new GridifyException($"{nameof(PageSize)} should be positive number."),
            > 500 when _validatePageSize => 500,
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
         OrderBy = null,
         Filter = Filter
      };
   }
   
   public void SetMaxPageSize()
   {
      _validatePageSize = false;
      PageSize = int.MaxValue;
   }
}