using Gridify;
using GridifyExtensions.Exceptions;

namespace GridifyExtensions.Models;

/// <summary>
///     Paged query model. Validates page/page-size and caps page size at 500 unless validation is disabled.
/// </summary>
public class GridifyQueryModel(bool validatePageSize) : GridifyQuery
{
    private bool _validatePageSize = validatePageSize;

    /// <summary>
    ///     Create a model with page-size validation enabled.
    /// </summary>
    public GridifyQueryModel() : this(true)
    {
    }

    /// <summary>
    ///     1-based page number. Must be positive.
    /// </summary>
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

    /// <summary>
    ///     Page size. Must be positive; capped at 500 when validation is enabled.
    /// </summary>
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

    /// <summary>
    ///     Comma-separated order-by expression. Falls back to the mapper's default when null.
    /// </summary>
    public new string? OrderBy
    {
        get => base.OrderBy;
        set => base.OrderBy = value;
    }

    /// <summary>
    ///     Gridify filter expression.
    /// </summary>
    public new string? Filter
    {
        get => base.Filter;
        set => base.Filter = value;
    }

    /// <summary>
    ///     Disable the page-size cap and select all rows on a single page.
    /// </summary>
    public void SetMaxPageSize()
    {
        _validatePageSize = false;
        PageSize = int.MaxValue;
        Page = 1;
    }
}
