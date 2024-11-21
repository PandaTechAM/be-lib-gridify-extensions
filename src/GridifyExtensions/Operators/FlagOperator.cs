using System.Linq.Expressions;
using Gridify.Syntax;

namespace GridifyExtensions.Operators;

internal class FlagOperator : IGridifyOperator
{
   public string GetOperator()
   {
      return "#hasFlag";
   }

   public Expression<OperatorParameter> OperatorHandler()
   {
      return (prop, value) => ((int)prop & (int)value) == (int)value;
   }
}