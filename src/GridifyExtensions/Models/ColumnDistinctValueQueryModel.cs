namespace GridifyExtensions.Models;

public class ColumnDistinctValueQueryModel : GridifyQueryModel
{
    public required string PropertyName { get; set; }
    public bool Encrypted { get; set; } = false;
}