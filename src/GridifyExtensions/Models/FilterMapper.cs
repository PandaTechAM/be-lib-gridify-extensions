using Gridify;
using GridifyExtensions.Abstractions;
using System.Linq.Expressions;

namespace GridifyExtensions.Models;

public class FilterMapper<T> : GridifyMapper<T>, IOrderThenBy
{
   internal const string Desc = " desc";
   internal const string Separator = ", ";

   private string _defaultOrderExpression = string.Empty;
   private readonly HashSet<string> _encryptedColumns = [];

   internal bool IsEncrypted(string column) => _encryptedColumns.Contains(column);

   internal string GetDefaultOrderExpression() => _defaultOrderExpression;

   public IOrderThenBy AddDefaultOrderBy(string column)
   {
      _defaultOrderExpression = column;
      return this;
   }

   public IOrderThenBy AddDefaultOrderByDescending(string column)
   {
      _defaultOrderExpression = column + Desc;
      return this;
   }

   IOrderThenBy IOrderThenBy.ThenBy(string column)
   {
      _defaultOrderExpression += Separator + column;

      return this;
   }

   IOrderThenBy IOrderThenBy.ThenByDescending(string column)
   {
      _defaultOrderExpression += Separator + column + Desc;

      return this;
   }

   public IGridifyMapper<T> AddMap(string from,
      Expression<Func<T, object?>> to,
      Func<string, object>? convertor = null,
      bool overrideIfExists = true,
      bool isEncrypted = false)
   {
      if (isEncrypted)
      {
         _encryptedColumns.Add(from);
      }


      return base.AddMap(from, to, convertor, overrideIfExists);
   }

   public IGridifyMapper<T> AddMap(string from,
      Expression<Func<T, int, object?>> to,
      Func<string, object>? convertor = null,
      bool overrideIfExists = true,
      bool isEncrypted = false)
   {
      if (isEncrypted)
      {
         _encryptedColumns.Add(from);
      }


      return base.AddMap(from, to, convertor, overrideIfExists);
   }
}