using Gridify;
using GridifyExtensions.Enums;
using GridifyExtensions.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace GridifyExtensions.Extensions;

public static class QueryableExtensions
{
    internal static Dictionary<Type, object> EntityGridifyMapperByType = [];

    public static async Task<PagedResponse<TDto>> FilterOrderAndGetPagedAsync<TEntity, TDto>(this IQueryable<TEntity> query, GridifyQueryModel model,
        Expression<Func<TEntity, TDto>> selectExpression, CancellationToken cancellationToken = default)
    where TEntity : class
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as GridifyMapper<TEntity>;

        query = query.ApplyFilteringAndOrdering(model, mapper);

        var totalCount = await query.CountAsync(cancellationToken);

        var dtoQuery = query.Select(selectExpression);

        dtoQuery = dtoQuery.ApplyPaging(model.Page, model.PageSize);

        return new PagedResponse<TDto>(await dtoQuery.ToListAsync(cancellationToken), model.Page, model.PageSize, totalCount);
    }

    public static Task<PagedResponse<TEntity>> FilterOrderAndGetPagedAsync<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model, CancellationToken cancellationToken = default)
        where TEntity : class
    => query.AsNoTracking().FilterOrderAndGetPagedAsync(model, x => x, cancellationToken);

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

    public static async Task<PagedResponse<TEntity>> GetPagedAsync<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var totalCount = await query.CountAsync(cancellationToken);

        query = query.ApplyPaging(model.Page, model.PageSize);

        return new PagedResponse<TEntity>(await query.ToListAsync(cancellationToken), model.Page, model.PageSize, totalCount);
    }

    public static Task<PagedResponse<object>> ColumnDistinctValuesAsync<TEntity>(this IQueryable<TEntity> query,
                                                                                 GridifyQueryModel model,
                                                                                 string columnName,
                                                                                 CancellationToken cancellationToken = default)
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as GridifyMapper<TEntity>;

        return query
                       .ApplyFiltering(model, mapper)
                       .ApplySelect(columnName)
                       .Distinct()
                       .OrderBy(x => x ?? 0)
                       .GetPagedAsync(model, cancellationToken);
    }

    public static async Task<object> AggregateAsync<TEntity>(this IQueryable<TEntity> query,
                                                                  AggregateQueryModel model,
                                                                  CancellationToken cancellationToken = default)
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

    public static IEnumerable<MappingModel> GetMappings<TEntity>()
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as GridifyMapper<TEntity>;

        return mapper!.GetCurrentMaps().Select(x => new MappingModel
        {
            Name = x.From,
            Type = x.To.Body is UnaryExpression ? (x.To.Body as UnaryExpression)!.Operand.Type.Name :
                           x.To.Body is MethodCallExpression ? ((x.To.Body as MethodCallExpression)!.Arguments?.LastOrDefault() as LambdaExpression)?.ReturnType.Name ?? x.To.Body.Type.Name
                         : x.To.Body.Type.Name,
        });
    }
}
