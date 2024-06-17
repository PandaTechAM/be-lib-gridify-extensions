using Gridify;
using System.Linq.Expressions;

namespace GridifyExtensions.Models;

public class FilterMapper<T> : GridifyMapper<T>
{
    private HashSet<string> _encryptedColumns { get; set; } = [];
    private HashSet<string> _arrayColumns { get; set; } = [];

    public bool IsArray(string column) => _arrayColumns.Contains(column);
    public bool IsEncrypted(string column) => _encryptedColumns.Contains(column);

    public IGridifyMapper<T> AddMap(string from,
                                        Expression<Func<T, object?>> to,
                                        Func<string, object>? convertor = null,
                                        bool overrideIfExists = true,
                                        bool isEncrypted = false,
                                        bool isArray = false)
    {
        if (isEncrypted)
        {
            _encryptedColumns.Add(from);
        }

        if (isArray)
        {
            _arrayColumns.Add(from);
        }

        return base.AddMap(from, to, convertor, overrideIfExists);
    }

    public IGridifyMapper<T> AddMap(string from,
        Expression<Func<T, int, object?>> to,
        Func<string, object>? convertor = null,
        bool overrideIfExists = true,
        bool isEncrypted = false,
        bool isArray = false)
    {
        if (isEncrypted)
        {
            _encryptedColumns.Add(from);
        }

        if (isArray)
        {
            _arrayColumns.Add(from);
        }

        return base.AddMap(from, to, convertor, overrideIfExists);
    }
}
