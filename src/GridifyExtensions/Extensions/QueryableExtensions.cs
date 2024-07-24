using Gridify;
using GridifyExtensions.Enums;
using GridifyExtensions.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace GridifyExtensions.Extensions;

public static class QueryableExtensions
{
    internal static Dictionary<Type, object> EntityGridifyMapperByType = [];

    public static async Task<PagedResponse<TDto>> FilterOrderAndGetPagedAsync<TEntity, TDto>(
        this IQueryable<TEntity> query, GridifyQueryModel model,
        Expression<Func<TEntity, TDto>> selectExpression, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as FilterMapper<TEntity>;

        model.OrderBy ??= mapper!.GetDefaultOrderExpression();

        query = query.ApplyFilteringAndOrdering(model, mapper);

        var totalCount = await query.CountAsync(cancellationToken);

        var dtoQuery = query.Select(selectExpression);

        dtoQuery = dtoQuery.ApplyPaging(model.Page, model.PageSize);

        return new PagedResponse<TDto>(await dtoQuery.ToListAsync(cancellationToken), model.Page, model.PageSize,
            totalCount);
    }

    public static Task<PagedResponse<TEntity>> FilterOrderAndGetPagedAsync<TEntity>(this IQueryable<TEntity> query,
        GridifyQueryModel model, CancellationToken cancellationToken = default)
        where TEntity : class
        => query.AsNoTracking().FilterOrderAndGetPagedAsync(model, x => x, cancellationToken);

    public static IQueryable<TEntity> ApplyFilter<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model)
        where TEntity : class
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as FilterMapper<TEntity>;

        return query.AsNoTracking().ApplyFiltering(model, mapper);
    }

    public static IQueryable<TEntity> ApplyOrder<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model)
        where TEntity : class
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as FilterMapper<TEntity>;

        model.OrderBy ??= mapper!.GetDefaultOrderExpression();

        return query.AsNoTracking().ApplyOrdering(model, mapper);
    }

    public static async Task<PagedResponse<TEntity>> GetPagedAsync<TEntity>(this IQueryable<TEntity> query,
        GridifyQueryModel model, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var totalCount = await query.CountAsync(cancellationToken);

        query = query.ApplyPaging(model.Page, model.PageSize);

        return new PagedResponse<TEntity>(await query.ToListAsync(cancellationToken), model.Page, model.PageSize,
            totalCount);
    }

    [Obsolete("Use ColumnDistinctValueCursoredQueryModel instead.")]
    public static async Task<PagedResponse<object>> ColumnDistinctValuesAsync<TEntity>(this IQueryable<TEntity> query,
        ColumnDistinctValueQueryModel model, Func<byte[], string>? decryptor = default,
        CancellationToken cancellationToken = default)
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as FilterMapper<TEntity>;

        if (!mapper!.IsEncrypted(model.PropertyName))
        {
            var result = await query
                .ApplyFiltering(model, mapper)
                .ApplySelect(model.PropertyName, mapper)
                .Distinct()
                .OrderBy(x => x)
                .GetPagedAsync(model, cancellationToken);

            return result;
        }

        var item = await query
            .ApplyFiltering(model, mapper)
            .Select(mapper.GetExpression(model.PropertyName))
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return new PagedResponse<object>([], 1, 1, 0);
        }

        var decryptedItem = decryptor!((byte[])item);
        return new PagedResponse<object>([decryptedItem], 1, 1, 1);
    }

    public static async Task<CursoredResponse<object>> ColumnDistinctValuesAsync<TEntity>(
        this IQueryable<TEntity> query,
        ColumnDistinctValueCursoredQueryModel model, Func<byte[], string>? decryptor = default,
        CancellationToken cancellationToken = default)
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as FilterMapper<TEntity>;

        var gridifyModel = model.ToGridifyQueryModel();

        if (!mapper!.IsEncrypted(model.PropertyName))
        {
            var result = await query
                .ApplyFiltering(gridifyModel, mapper)
                .ApplySelect(model.PropertyName, mapper)
                .Distinct()
                .OrderBy(x => x)
                .Take(model.PageSize)
                .ToListAsync(cancellationToken: cancellationToken);

            return new CursoredResponse<object>(result, model.PageSize);
        }

        var item = await query
            .ApplyFiltering(gridifyModel, mapper)
            .Select(mapper.GetExpression(model.PropertyName))
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return new CursoredResponse<object>([], model.PageSize);
        }

        var decryptedItem = decryptor!((byte[])item);
        return new CursoredResponse<object>([decryptedItem], model.PageSize);
    }

    public static async Task<object> AggregateAsync<TEntity>(this IQueryable<TEntity> query,
        AggregateQueryModel model,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var aggregateProperty = model.PropertyName;

        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as FilterMapper<TEntity>;

        var filteredQuery = query.ApplyFiltering(model, mapper).ApplySelect(aggregateProperty, mapper);

        return model.AggregateType switch
        {
            AggregateType.UniqueCount => await filteredQuery.Distinct().CountAsync(cancellationToken),
            AggregateType.Sum => await filteredQuery.SumAsync(x => (decimal)x, cancellationToken),
            AggregateType.Average => await filteredQuery.AverageAsync(x => (decimal)x, cancellationToken),
            AggregateType.Min => await filteredQuery.MinAsync(cancellationToken),
            AggregateType.Max => await filteredQuery.MaxAsync(cancellationToken),
            _ => throw new NotImplementedException(),
        };
    }

    public static IEnumerable<MappingModel> GetMappings<TEntity>()
    {
        var mapper = EntityGridifyMapperByType[typeof(TEntity)] as FilterMapper<TEntity>;

        return mapper!.GetCurrentMaps().Select(x => new MappingModel
        {
            Name = x.From,
            Type = x.To.Body is UnaryExpression
                ? (x.To.Body as UnaryExpression)!.Operand.Type.Name
                : x.To.Body is MethodCallExpression
                    ? ((x.To.Body as MethodCallExpression)!.Arguments.LastOrDefault() as LambdaExpression)?.ReturnType
                    .Name ?? x.To.Body.Type.Name
                    : x.To.Body.Type.Name,
        });
    }
}