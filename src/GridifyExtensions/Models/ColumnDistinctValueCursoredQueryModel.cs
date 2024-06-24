using GridifyExtensions.Exceptions;

namespace GridifyExtensions.Models;

public class ColumnDistinctValueCursoredQueryModel
{
    private int _pageSize = 10;

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

    public required string PropertyName { get; set; }
    public string? Filter { get; set; }
    
    public GridifyQueryModel ToGridifyQueryModel()
    {
        return new GridifyQueryModel
        {
            Page = 1,
            PageSize = PageSize,
            Filter = Filter
        };
    }
}