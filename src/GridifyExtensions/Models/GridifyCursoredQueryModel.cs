using GridifyExtensions.Exceptions;

namespace GridifyExtensions.Models;

/// <summary>
///     Cursor-based query model. Validates page size and caps it at 500 unless validation is disabled.
/// </summary>
public class GridifyCursoredQueryModel(bool validatePageSize)
{
    private int _pageSize = 20;

    private bool _validatePageSize = validatePageSize;

    /// <summary>
    ///     Create a model with page-size validation enabled.
    /// </summary>
    public GridifyCursoredQueryModel() : this(true)
    {
    }

    /// <summary>
    ///     Page size. Must be positive; capped at 500 when validation is enabled. Defaults to 20.
    /// </summary>
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

    /// <summary>
    ///     Gridify filter expression.
    /// </summary>
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

    /// <summary>
    ///     Disable the page-size cap and select all rows on a single page.
    /// </summary>
    public void SetMaxPageSize()
    {
        _validatePageSize = false;
        PageSize = int.MaxValue;
    }
}
