using GridifyExtensions.Exceptions;

namespace GridifyExtensions.Models;

public class ColumnDistinctValueCursoredQueryModel : GridifyCursoredQueryModel
{
    public required string PropertyName { get; set; }
}