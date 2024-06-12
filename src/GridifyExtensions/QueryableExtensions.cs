using Gridify;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace GridifyExtensions;

public static class QueryableExtensions
{
    internal static Dictionary<Type, object> EntityGridifyMapperByType = [];

    public static async Task<PagedResponse<TDto>> FilterOrderAndGetPagedAsync<TEntity, TDto>(this IQueryable<TEntity> query, GridifyQueryModel model,
        Expression<Func<TEntity, TDto>> selectExpression, CancellationToken cancellationToken)
    where TEntity : class
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as GridifyMapper<TEntity>;

        query = query.ApplyFilteringAndOrdering(model, mapper);

        var totalCount = await query.CountAsync(cancellationToken);

        var dtoQuery = query.Select(selectExpression);

        dtoQuery = dtoQuery.ApplyPaging(model.Page, model.PageSize);

        return new PagedResponse<TDto>(await dtoQuery.ToListAsync(cancellationToken), model.Page, model.PageSize, totalCount);
    }

    public static Task<PagedResponse<TEntity>> FilterOrderAndGetPagedAsync<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model, CancellationToken cancellationToken)
        where TEntity : class
    => FilterOrderAndGetPagedAsync(query.AsNoTracking(), model, x => x, cancellationToken);

    public static IQueryable<TEntity> ApplyFilter<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model)
        where TEntity : class
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as GridifyMapper<TEntity>;

        return query.AsNoTracking().ApplyFiltering(model, mapper);
    }

    public static IQueryable<TEntity> ApplyOrder<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model)
        where TEntity : class
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as GridifyMapper<TEntity>;

        return query.AsNoTracking().ApplyOrdering(model, mapper);
    }

    public static async Task<PagedResponse<TEntity>> GetPagedAsync<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model, CancellationToken cancellationToken)
        where TEntity : class
    {
        var totalCount = await query.CountAsync(cancellationToken);

        query = query.ApplyPaging(model.Page, model.PageSize);

        return new PagedResponse<TEntity>(await query.ToListAsync(cancellationToken), model.Page, model.PageSize, totalCount);
    }

    public static Task<PagedResponse<object>> ColumnDistinctValues<TEntity>(this IQueryable<TEntity> query,
                                                                                 GridifyQueryModel model,
                                                                                 string columnName,
                                                                                 CancellationToken cancellationToken)
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as GridifyMapper<TEntity>;

        return query.ApplyFilteringAndOrdering(model, mapper)
                    .ApplySelect(columnName)
                    .Distinct()
                    .GetPagedAsync(model, cancellationToken);
    }

    public static async Task<object> AggregateAsync<TEntity>(this IQueryable<TEntity> query,
                                                                  AggregateModel model,
                                                                  CancellationToken cancellationToken)
        where TEntity : class
    {
        var aggregateProperty = model.PropertyName;

        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as GridifyMapper<TEntity>;

        var query2 = query.ApplyFiltering(model, mapper).ApplySelect(aggregateProperty, mapper);

        return model.AggregateType switch
        {
            AggregateType.UniqueCount => await query2.Distinct().CountAsync(cancellationToken),
            AggregateType.Sum => await query2.SumAsync(x => (decimal)x, cancellationToken),
            AggregateType.Average => await query2.AverageAsync(x => (decimal)x, cancellationToken),
            AggregateType.Min => await query2.MinAsync(cancellationToken),
            AggregateType.Max => await query2.MaxAsync(cancellationToken),
            _ => throw new NotImplementedException(),
        };
    }
}
