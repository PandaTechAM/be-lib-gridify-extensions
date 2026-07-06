using System.Linq.Expressions;
using Gridify;
using GridifyExtensions.Abstractions;

namespace GridifyExtensions.Models;

/// <summary>
///     Gridify mapper with default ordering, encrypted-column tracking, and a fluent order-by API.
/// </summary>
public class FilterMapper<T> : GridifyMapper<T>, IOrderThenBy
{
    private const string Desc = " desc";
    private const string Separator = ", ";
    private readonly HashSet<string> _encryptedColumns = [];

    private string _defaultOrderExpression = string.Empty;

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

    internal bool IsEncrypted(string column)
    {
        return _encryptedColumns.Contains(column);
    }

    internal string GetDefaultOrderExpression()
    {
        return _defaultOrderExpression;
    }

    /// <summary>
    ///     Set the default ascending order-by column, replacing any existing default.
    /// </summary>
    public IOrderThenBy AddDefaultOrderBy(string column)
    {
        _defaultOrderExpression = column;
        return this;
    }

    /// <summary>
    ///     Set the default descending order-by column, replacing any existing default.
    /// </summary>
    public IOrderThenBy AddDefaultOrderByDescending(string column)
    {
        _defaultOrderExpression = column + Desc;
        return this;
    }

    /// <summary>
    ///     Add a property mapping, optionally flagging the column as encrypted.
    /// </summary>
    public IGridifyMapper<T> AddMap(string from,
        Expression<Func<T, object?>> to,
        Func<string, object>? converter = null,
        bool overrideIfExists = true,
        bool isEncrypted = false)
    {
        if (isEncrypted)
        {
            _encryptedColumns.Add(from);
        }

        return base.AddMap(from, to, converter, overrideIfExists);
    }

    /// <summary>
    ///     Add an indexed property mapping, optionally flagging the column as encrypted.
    /// </summary>
    public IGridifyMapper<T> AddMap(string from,
        Expression<Func<T, int, object?>> to,
        Func<string, object>? converter = null,
        bool overrideIfExists = true,
        bool isEncrypted = false)
    {
        if (isEncrypted)
        {
            _encryptedColumns.Add(from);
        }

        return base.AddMap(from, to, converter, overrideIfExists);
    }
}
