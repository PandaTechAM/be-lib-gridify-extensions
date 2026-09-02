using System.Collections;
using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Gridify;
using GridifyExtensions.Enums;
using GridifyExtensions.Exceptions;
using GridifyExtensions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GridifyExtensions.Extensions;

/// <summary>
///     Extension methods for IQueryable to support Gridify filtering, ordering, pagination, and aggregation.
/// </summary>
public static class QueryableExtensions
{
    internal static FrozenDictionary<Type, object> EntityGridifyMapperByType = FrozenDictionary<Type, object>.Empty;

    // ---------- Core helpers ----------
    private static FilterMapper<TEntity> RequireMapper<TEntity>()
        where TEntity : class
    {
        if (!EntityGridifyMapperByType.TryGetValue(typeof(TEntity), out var raw) ||
            raw is not FilterMapper<TEntity> mapper)
        {
            throw new KeyNotFoundException($"No FilterMapper registered for entity type {typeof(TEntity).Name}.");
        }

        return mapper;
    }

    // ---------- Filtering / Ordering ----------

    /// <summary>
    ///     Apply filtering from a GridifyQueryModel.
    /// </summary>
    public static IQueryable<TEntity> ApplyFilter<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model)
        where TEntity : class
    {
        return query.ApplyFiltering(model, RequireMapper<TEntity>());
    }

    /// <summary>
    ///     Apply filtering from a filter string.
    /// </summary>
    public static IQueryable<TEntity> ApplyFilter<TEntity>(this IQueryable<TEntity> query, string filter)
        where TEntity : class
    {
        var model = new GridifyQueryModel
        {
            Page = 1,
            PageSize = 1,
            OrderBy = null,
            Filter = filter
        };
        return query.ApplyFiltering(model, RequireMapper<TEntity>());
    }

    /// <summary>
    ///     Apply ordering from a GridifyQueryModel.
    /// </summary>
    public static IQueryable<TEntity> ApplyOrder<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model)
        where TEntity : class
    {
        var mapper = RequireMapper<TEntity>();
        model.OrderBy ??= mapper.GetDefaultOrderExpression();
        return query.AsNoTracking()
            .ApplyOrdering(model, mapper);
    }

    /// <summary>
    ///     Get paged results with total count.
    /// </summary>
    public static async Task<PagedResponse<TEntity>> GetPagedAsync<TEntity>(this IQueryable<TEntity> query,
        GridifyQueryModel model,
        CancellationToken ct = default)
        where TEntity : class
    {
        var totalCount = await query.CountAsync(ct);
        query = query.ApplyPaging(model.Page, model.PageSize);
        var data = await query.ToListAsync(ct);
        return new PagedResponse<TEntity>(data, model.Page, model.PageSize, totalCount);
    }

    /// <summary>
    ///     Get paged results with projection and total count.
    /// </summary>
    public static async Task<PagedResponse<TDto>> GetPagedAsync<TEntity, TDto>(this IQueryable<TEntity> query,
        GridifyQueryModel model,
        Expression<Func<TEntity, TDto>> selectExpression,
        CancellationToken ct = default)
        where TEntity : class
    {
        var totalCount = await query.CountAsync(ct);
        var data = await query.Select(selectExpression)
            .ApplyPaging(model.Page, model.PageSize)
            .ToListAsync(ct);
        return new PagedResponse<TDto>(data, model.Page, model.PageSize, totalCount);
    }

    /// <summary>
    ///     Apply filtering, ordering, and pagination with projection.
    /// </summary>
    public static async Task<PagedResponse<TDto>> FilterOrderAndGetPagedAsync<TEntity, TDto>(
        this IQueryable<TEntity> query,
        GridifyQueryModel model,
        Expression<Func<TEntity, TDto>> selectExpression,
        CancellationToken ct = default)
        where TEntity : class
    {
        try
        {
            var mapper = RequireMapper<TEntity>();
            model.OrderBy ??= mapper.GetDefaultOrderExpression();

            var filtered = query.ApplyFiltering(model, mapper);
            var totalCount = await filtered.CountAsync(ct);
            var ordered = filtered.ApplyOrdering(model, mapper);

            var data = await ordered
                .Select(selectExpression)
                .ApplyPaging(model.Page, model.PageSize)
                .ToListAsync(ct);

            return new PagedResponse<TDto>(data, model.Page, model.PageSize, totalCount);
        }
        catch (Exception ex) when (
            ex is GridifyFilteringException ||
            ex is FormatException ||
            ex is ArgumentException)
        {
            throw new GridifyException($"Error applying filtering, ordering, and pagination: {ex.Message}");
        }
    }

    /// <summary>
    ///     Apply filtering, ordering, and pagination without projection.
    /// </summary>
    public static Task<PagedResponse<TEntity>> FilterOrderAndGetPagedAsync<TEntity>(this IQueryable<TEntity> query,
        GridifyQueryModel model,
        CancellationToken ct = default)
        where TEntity : class
    {
        return query.AsNoTracking()
            .FilterOrderAndGetPagedAsync(model, x => x, ct);
    }

    /// <summary>
    ///     Apply filtering, ordering, and cursor-based pagination with projection.
    /// </summary>
    public static async Task<CursoredResponse<TDto>> FilterOrderAndGetCursoredAsync<TEntity, TDto>(
        this IQueryable<TEntity> query,
        GridifyCursoredQueryModel model,
        Expression<Func<TEntity, TDto>> selectExpression,
        CancellationToken ct = default)
        where TEntity : class
    {
        try
        {
            var mapper = RequireMapper<TEntity>();

            var queryModel = model.ToGridifyQueryModel();
            queryModel.OrderBy ??= mapper.GetDefaultOrderExpression();

            query = query.ApplyFilteringAndOrdering(queryModel, mapper);

            var data = await query.Select(selectExpression)
                .Take(model.PageSize)
                .ToListAsync(ct);

            return new CursoredResponse<TDto>(data, model.PageSize);
        }
        catch (Exception ex) when (
            ex is GridifyFilteringException ||
            ex is FormatException ||
            ex is ArgumentException)
        {
            throw new GridifyException($"Error applying filtering, ordering, and pagination: {ex.Message}");
        }
    }

    /// <summary>
    ///     Apply filtering, ordering, and cursor-based pagination without projection.
    /// </summary>
    public static Task<CursoredResponse<TEntity>> FilterOrderAndGetCursoredAsync<TEntity>(
        this IQueryable<TEntity> query,
        GridifyCursoredQueryModel model,
        CancellationToken ct = default)
        where TEntity : class
    {
        return query.AsNoTracking()
            .FilterOrderAndGetCursoredAsync(model, x => x, ct);
    }

    /// <summary>
    ///     Get distinct values for a column with cursor pagination.
    /// </summary>
    public static async Task<CursoredResponse<object?>> ColumnDistinctValuesAsync<TEntity>(
        this IQueryable<TEntity> query,
        ColumnDistinctValueCursoredQueryModel model,
        Func<byte[], string>? decryptor = null,
        CancellationToken ct = default)
        where TEntity : class
    {
        try
        {
            var mapper = RequireMapper<TEntity>();
            var gridifyModel = model.ToGridifyQueryModel();

            if (!mapper.IsEncrypted(model.PropertyName))
            {
                var selectedNonEncrypted = query
                    .ApplyFiltering(gridifyModel, mapper)
                    .ApplySelect(model.PropertyName, mapper)
                    .Distinct();

                var term = ExtractStarContainsTerm(model.Filter, model.PropertyName);
                if (!string.IsNullOrEmpty(term) && IsStringColumn(query, mapper, model.PropertyName))
                {
                    var termLower = term.ToLower();

                    var projected = query
                        .ApplyFiltering(gridifyModel, mapper)
                        .Select(StringSelector(query, mapper, model.PropertyName))
                        .Distinct();

                    var data = await projected
                        .OrderBy(x => x == null ? 0 : 1)
                        .ThenBy(x => x != null && x.ToLower() == termLower ? 0 : 1)
                        .ThenBy(x => x == null ? int.MaxValue : x.Length)
                        .ThenBy(x => x)
                        .Take(model.PageSize)
                        .ToListAsync(ct);

                    return new CursoredResponse<object?>(data.Cast<object?>()
                            .ToList(),
                        model.PageSize);
                }

                var data2 = await selectedNonEncrypted
                    .OrderBy(x => (object?)x == null ? 0 : 1)
                    .Take(model.PageSize)
                    .ToListAsync(ct);

                return new CursoredResponse<object?>(data2!, model.PageSize);
            }

            // Encrypted path
            var encryptedQuery = query
                .ApplyFiltering(gridifyModel, mapper)
                .ApplySelect(model.PropertyName, mapper);

            if (string.IsNullOrWhiteSpace(model.Filter))
            {
                bool hasNullLike;
                try
                {
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                    hasNullLike = await encryptedQuery.AnyAsync(x => x == null, ct);
                }
                catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
                {
                    hasNullLike = true;
                }

                return hasNullLike
                    ? new CursoredResponse<object?>([null], model.PageSize)
                    : new CursoredResponse<object?>([], model.PageSize);
            }

            var selected = await encryptedQuery.FirstOrDefaultAsync(ct);
            switch (selected)
            {
                case null:
                case byte[] { Length: 0 }:
                    return new CursoredResponse<object?>([null], model.PageSize);
                case byte[] when decryptor == null:
                    throw new KeyNotFoundException("Decryptor is required for encrypted properties.");
                case byte[] sb:
                    return new CursoredResponse<object?>([decryptor(sb)], model.PageSize);
            }

            if (selected is not IEnumerable<byte[]> seq)
            {
                throw new InvalidCastException(
                    "Encrypted selector did not return a byte[] or IEnumerable<byte[]> value.");
            }

            var ng = ((IEnumerable)seq).GetEnumerator();
            using var ng1 = ng as IDisposable;
            if (!ng.MoveNext())
            {
                return new CursoredResponse<object?>([null], model.PageSize);
            }

            var firstObj = ng.Current;
            if (firstObj is not byte[] first || first.Length == 0)
            {
                return new CursoredResponse<object?>([null], model.PageSize);
            }

            return decryptor == null
                ? throw new KeyNotFoundException("Decryptor is required for encrypted properties.")
                : new CursoredResponse<object?>([decryptor(first)], model.PageSize);
        }
        catch (Exception ex) when (
            ex is GridifyFilteringException ||
            ex is FormatException ||
            ex is ArgumentException)
        {
            throw new GridifyException($"Error applying filtering and getting distinct values: {ex.Message}");
        }
    }

    /// <summary>
    ///     Get distinct values for a column with Page/PageSize (offset) pagination. Returns a
    ///     <see cref="PagedResponse{T}" /> with the total distinct-value count, the same paged response the
    ///     rest of the paged endpoints use.
    /// </summary>
    public static async Task<PagedResponse<object?>> ColumnDistinctValuesAsync<TEntity>(
        this IQueryable<TEntity> query,
        ColumnDistinctValueQueryModel model,
        Func<byte[], string>? decryptor = null,
        CancellationToken ct = default)
        where TEntity : class
    {
        try
        {
            var mapper = RequireMapper<TEntity>();
            var gridifyModel = model;

            if (!mapper.IsEncrypted(model.PropertyName))
            {
                var skip = (model.Page - 1) * model.PageSize;

                // Keyed path: distinct values ordered by an opt-in natural sort key column, offset paged.
                // Distinct is taken on the (value, key) pair so key collisions never drop a value, and the raw
                // value is the final deterministic tie-break.
                if (mapper.TryGetDistinctOrderKey(model.PropertyName, out var orderKeyColumn))
                {
                    var selector = BuildDistinctRowSelector(mapper, model.PropertyName, orderKeyColumn);

                    var rows = query
                        .ApplyFiltering(gridifyModel, mapper)
                        .Select(selector)
                        .Distinct();

                    var keyedTerm = ExtractStarContainsTerm(model.Filter, model.PropertyName);
                    var valueIsString = IsStringColumn(query, mapper, model.PropertyName);

                    var keyedTotal = await rows.LongCountAsync(ct);
                    var keyedData = await OrderDistinctRows(rows, keyedTerm, valueIsString)
                        .Skip(skip)
                        .Take(model.PageSize)
                        .Select(r => r.Value)
                        .ToListAsync(ct);

                    return new PagedResponse<object?>(keyedData, model.Page, model.PageSize, keyedTotal);
                }

                var selectedNonEncrypted = query
                    .ApplyFiltering(gridifyModel, mapper)
                    .ApplySelect(model.PropertyName, mapper)
                    .Distinct();

                var term = ExtractStarContainsTerm(model.Filter, model.PropertyName);
                if (!string.IsNullOrEmpty(term) && IsStringColumn(query, mapper, model.PropertyName))
                {
                    var termLower = term.ToLower();

                    var projected = query
                        .ApplyFiltering(gridifyModel, mapper)
                        .Select(StringSelector(query, mapper, model.PropertyName))
                        .Distinct();

                    var searchTotal = await projected.LongCountAsync(ct);
                    var data = await projected
                        .OrderBy(x => x == null ? 0 : 1)
                        .ThenBy(x => x != null && x.ToLower() == termLower ? 0 : 1)
                        .ThenBy(x => x == null ? int.MaxValue : x.Length)
                        .ThenBy(x => x)
                        .Skip(skip)
                        .Take(model.PageSize)
                        .ToListAsync(ct);

                    return new PagedResponse<object?>(data.Cast<object?>()
                            .ToList(),
                        model.Page,
                        model.PageSize,
                        searchTotal);
                }

                // No key, no search: deterministic order (null first, then value ascending). Offset paged.
                var noSearchTotal = await selectedNonEncrypted.LongCountAsync(ct);
                var data2 = await selectedNonEncrypted
                    .OrderBy(x => x == null ? 0 : 1)
                    .ThenBy(x => x)
                    .Skip(skip)
                    .Take(model.PageSize)
                    .ToListAsync(ct);

                return new PagedResponse<object?>(data2!, model.Page, model.PageSize, noSearchTotal);
            }

            // Encrypted path: at most one representative value is returned (encrypted columns cannot be paged
            // meaningfully), so the total count is simply the number of values returned.
            var encryptedQuery = query
                .ApplyFiltering(gridifyModel, mapper)
                .ApplySelect(model.PropertyName, mapper);

            if (string.IsNullOrWhiteSpace(model.Filter))
            {
                bool hasNullLike;
                try
                {
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                    hasNullLike = await encryptedQuery.AnyAsync(x => x == null, ct);
                }
                catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
                {
                    hasNullLike = true;
                }

                return hasNullLike
                    ? new PagedResponse<object?>([null], model.Page, model.PageSize, 1)
                    : new PagedResponse<object?>([], model.Page, model.PageSize, 0);
            }

            var selected = await encryptedQuery.FirstOrDefaultAsync(ct);
            switch (selected)
            {
                case null:
                case byte[] { Length: 0 }:
                    return new PagedResponse<object?>([null], model.Page, model.PageSize, 1);
                case byte[] when decryptor == null:
                    throw new KeyNotFoundException("Decryptor is required for encrypted properties.");
                case byte[] sb:
                    return new PagedResponse<object?>([decryptor(sb)], model.Page, model.PageSize, 1);
            }

            if (selected is not IEnumerable<byte[]> seq)
            {
                throw new InvalidCastException(
                    "Encrypted selector did not return a byte[] or IEnumerable<byte[]> value.");
            }

            var ng = ((IEnumerable)seq).GetEnumerator();
            using var ng1 = ng as IDisposable;
            if (!ng.MoveNext())
            {
                return new PagedResponse<object?>([null], model.Page, model.PageSize, 1);
            }

            var firstObj = ng.Current;
            if (firstObj is not byte[] first || first.Length == 0)
            {
                return new PagedResponse<object?>([null], model.Page, model.PageSize, 1);
            }

            return decryptor == null
                ? throw new KeyNotFoundException("Decryptor is required for encrypted properties.")
                : new PagedResponse<object?>([decryptor(first)], model.Page, model.PageSize, 1);
        }
        catch (Exception ex) when (
            ex is GridifyFilteringException ||
            ex is FormatException ||
            ex is ArgumentException)
        {
            throw new GridifyException($"Error applying filtering and getting distinct values: {ex.Message}");
        }
    }

    /// <summary>
    ///     Perform aggregation operations on a property.
    /// </summary>
    public static async Task<object> AggregateAsync<TEntity>(this IQueryable<TEntity> query,
        AggregateQueryModel model,
        CancellationToken ct = default)
        where TEntity : class
    {
        var mapper = RequireMapper<TEntity>();
        var filtered = query.ApplyFiltering(model.ToGridifyQueryModel(), mapper)
            .ApplySelect(model.PropertyName, mapper);

        return model.AggregateType switch
        {
            AggregateType.UniqueCount => await filtered.Distinct()
                .CountAsync(ct),
            AggregateType.Sum => await filtered.SumAsync(x => Math.Round((decimal)x!, 8), ct),
            AggregateType.Average => await filtered.AverageAsync(x => Math.Round((decimal)x!, 8), ct),
            AggregateType.Min => await filtered.MinAsync(ct)!,
            AggregateType.Max => await filtered.MaxAsync(ct)!,
            _ => throw new NotImplementedException()
        };
    }

    /// <summary>
    ///     Get available property mappings for an entity type.
    /// </summary>
    public static IEnumerable<MappingModel> GetMappings<TEntity>()
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as FilterMapper<TEntity>;

        return mapper!.GetCurrentMaps()
            .Select(x => new MappingModel(
                x.From,
                x.To.Body switch
                {
                    UnaryExpression ue => ue.Operand.Type.Name,
                    MethodCallExpression mc => (mc.Arguments.LastOrDefault() as LambdaExpression)?.ReturnType.Name
                                               ?? x.To.Body.Type.Name,
                    _ => x.To.Body.Type.Name
                }));
    }

    // ---------- Private helpers ----------

    private static Expression<Func<TEntity, string?>> EfStringSelector<TEntity>(string propertyName)
        where TEntity : class
    {
        var e = Expression.Parameter(typeof(TEntity), "e");
        var body = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(string)],
            e,
            Expression.Constant(propertyName));

        return Expression.Lambda<Func<TEntity, string?>>(body, e);
    }

    private static string? ExtractStarContainsTerm(string? filter, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var m = Regex.Match(
            filter,
            $@"(?i)\b{Regex.Escape(propertyName)}\s*=\s*\*(?<term>[^;,)]+)");

        if (!m.Success)
        {
            return null;
        }

        var term = m.Groups["term"]
            .Value
            .Trim();
        return term.Length == 0 ? null : term;
    }

    private static bool IsStringColumn<TEntity>(IQueryable<TEntity> query, FilterMapper<TEntity> mapper, string name)
        where TEntity : class
    {
        var db = TryGetDbContext(query);
        var et = db?.Model.FindEntityType(typeof(TEntity));
        var p = et?.FindProperty(name);
        if (p != null)
        {
            return p.ClrType == typeof(string);
        }

        var map = mapper.GetCurrentMaps()
            .FirstOrDefault(m => m.From == name);
        if (map == null)
        {
            return false;
        }

        var body = map.To.Body is UnaryExpression { NodeType: ExpressionType.Convert } ue ? ue.Operand : map.To.Body;
        return body.Type == typeof(string);
    }

    private static Expression<Func<TEntity, string?>> StringSelector<TEntity>(IQueryable<TEntity> query,
        FilterMapper<TEntity> mapper,
        string name)
        where TEntity : class
    {
        var db = TryGetDbContext(query);
        var et = db?.Model.FindEntityType(typeof(TEntity));
        var p = et?.FindProperty(name);

        if (p != null)
        {
            return EfStringSelector<TEntity>(name);
        }

        var map = mapper.GetCurrentMaps()
                      .FirstOrDefault(m => m.From == name)
                  ?? throw new KeyNotFoundException($"No map found for '{name}'.");

        var param = map.To.Parameters[0];
        var body = map.To.Body is UnaryExpression { NodeType: ExpressionType.Convert } ue ? ue.Operand : map.To.Body;

        return body.Type != typeof(string)
            ? throw new InvalidOperationException($"Map '{name}' must return string. Actual: {body.Type}.")
            : Expression.Lambda<Func<TEntity, string?>>(body, param);
    }

    private static DbContext? TryGetDbContext<TEntity>(IQueryable<TEntity> query)
    {
        if (query is not IInfrastructure<IServiceProvider> infra)
        {
            return null;
        }

        return infra.Instance.GetService<ICurrentDbContext>()
            ?.Context;
    }
    // ---------- Keyed distinct-values helpers ----------

    /// <summary>
    ///     Build <c>e =&gt; new DistinctRow { Value = &lt;valueMap&gt;, Key = &lt;keyMap&gt; }</c> from the two
    ///     registered map expressions, sharing a single parameter so EF can translate the projection.
    /// </summary>
    private static Expression<Func<TEntity, DistinctRow>> BuildDistinctRowSelector<TEntity>(
         FilterMapper<TEntity> mapper,
         string valueColumn,
         string keyColumn)
         where TEntity : class
    {
        var maps = mapper.GetCurrentMaps()
            .ToList();
        var valueMap = maps.FirstOrDefault(m => m.From == valueColumn)
                       ?? throw new KeyNotFoundException($"No map found for '{valueColumn}'.");
        var keyMap = maps.FirstOrDefault(m => m.From == keyColumn)
                     ?? throw new KeyNotFoundException($"No map found for '{keyColumn}'.");

        var param = Expression.Parameter(typeof(TEntity), "e");
        var valueBody = ReplaceParameter(valueMap.To.Body, valueMap.To.Parameters[0], param);
        var keyBody = ReplaceParameter(keyMap.To.Body, keyMap.To.Parameters[0], param);

        var rowType = typeof(DistinctRow);
        var init = Expression.MemberInit(
            Expression.New(rowType),
            Expression.Bind(rowType.GetProperty(nameof(DistinctRow.Value))!, EnsureObject(valueBody)),
            Expression.Bind(rowType.GetProperty(nameof(DistinctRow.Key))!, EnsureObject(keyBody)));

        return Expression.Lambda<Func<TEntity, DistinctRow>>(init, param);
    }

    /// <summary>
    ///     Order distinct rows: null values first, then (when searching a string column) values that start with
    ///     the term, then by the natural sort key, then by the raw value as a deterministic tie-break for key
    ///     collisions.
    /// </summary>
    private static IOrderedQueryable<DistinctRow> OrderDistinctRows(
         IQueryable<DistinctRow> rows,
         string? term,
         bool valueIsString)
    {
        var termLower = term?.ToLower();

        if (!string.IsNullOrEmpty(termLower) && valueIsString)
        {
            return rows
                .OrderBy(r => r.Value == null ? 0 : 1)
                .ThenBy(r => r.Value != null && ((string)r.Value).ToLower()
                    .StartsWith(termLower)
                    ? 0
                    : 1)
                .ThenBy(r => r.Key)
                .ThenBy(r => r.Value);
        }

        return rows
            .OrderBy(r => r.Value == null ? 0 : 1)
            .ThenBy(r => r.Key)
            .ThenBy(r => r.Value);
    }

    private static Expression EnsureObject(Expression expression)
    {
        return expression.Type == typeof(object) ? expression : Expression.Convert(expression, typeof(object));
    }

    private static Expression ReplaceParameter(Expression body, ParameterExpression from, ParameterExpression to)
    {
        return new ParameterReplaceVisitor(from, to).Visit(body);
    }

    private sealed class DistinctRow
    {
        public object? Value { get; set; }
        public object? Key { get; set; }
    }

    private sealed class ParameterReplaceVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == from ? to : base.VisitParameter(node);
        }
    }

}
