using Gridify;
using GridifyExtensions.Exceptions;

namespace GridifyExtensions.Models;

public class GridifyQueryModel(bool validatePageSize) : GridifyQuery
{
   private bool _validatePageSize = validatePageSize;

   public GridifyQueryModel() : this(true)
   {
   }

   public new required int Page
   {
      get => base.Page;
      set
      {
         if (value <= 0)
         {
            throw new GridifyException($"{nameof(Page)} should be positive number.");
         }

         base.Page = value;
      }
   }

   public new required int PageSize
   {
      get => base.PageSize;
      set
      {
         value = value switch
         {
            <= 0 => throw new GridifyException($"{nameof(PageSize)} should be positive number."),
            > 500 when _validatePageSize => 500,
            _ => value
         };

         base.PageSize = value;
      }
   }

   public new string? OrderBy
   {
      get => base.OrderBy;
      set => base.OrderBy = value;
   }

   public new string? Filter
   {
      get => base.Filter;
      set => base.Filter = value;
   }

   public void SetMaxPageSize()
   {
      _validatePageSize = false;
      PageSize = int.MaxValue;
   }
}