using System.Linq.Expressions;
using Gridify;
using GridifyExtensions.Enums;
using GridifyExtensions.Models;
using Microsoft.EntityFrameworkCore;

namespace GridifyExtensions.Extensions;

public static class QueryableExtensions
{
   internal static Dictionary<Type, object> EntityGridifyMapperByType = [];

   // ---------- Core helpers ----------


   private static Expression<Func<T, object>> CreateSelector<T>(string propertyName)
   {
      var p = Expression.Parameter(typeof(T), "x");
      var body = Expression.Convert(Expression.Property(p, propertyName), typeof(object));
      return Expression.Lambda<Func<T, object>>(body, p);
   }

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
   public static IQueryable<TEntity> ApplyFilter<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model)
      where TEntity : class =>
      query.ApplyFiltering(model, RequireMapper<TEntity>());

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

   public static IQueryable<TEntity> ApplyOrder<TEntity>(this IQueryable<TEntity> query, GridifyQueryModel model)
      where TEntity : class
   {
      var mapper = RequireMapper<TEntity>();
      model.OrderBy ??= mapper.GetDefaultOrderExpression();
      return query.AsNoTracking()
                  .ApplyOrdering(model, mapper);
   }

   // ---------- Paging (simple) ----------
   public static async Task<PagedResponse<TEntity>> GetPagedAsync<TEntity>(this IQueryable<TEntity> query,
      GridifyQueryModel model,
      CancellationToken cancellationToken = default)
      where TEntity : class
   {
      var totalCount = await query.CountAsync(cancellationToken);
      query = query.ApplyPaging(model.Page, model.PageSize);
      var data = await query.ToListAsync(cancellationToken);
      return new PagedResponse<TEntity>(data, model.Page, model.PageSize, totalCount);
   }

   public static async Task<PagedResponse<TDto>> GetPagedAsync<TEntity, TDto>(this IQueryable<TEntity> query,
      GridifyQueryModel model,
      Expression<Func<TEntity, TDto>> selectExpression,
      CancellationToken cancellationToken = default)
      where TEntity : class
   {
      var totalCount = await query.CountAsync(cancellationToken);
      var data = await query.Select(selectExpression)
                            .ApplyPaging(model.Page, model.PageSize)
                            .ToListAsync(cancellationToken);
      return new PagedResponse<TDto>(data, model.Page, model.PageSize, totalCount);
   }

   // ---------- Filter + Order + Paging ----------
   public static async Task<PagedResponse<TDto>> FilterOrderAndGetPagedAsync<TEntity, TDto>(
      this IQueryable<TEntity> query,
      GridifyQueryModel model,
      Expression<Func<TEntity, TDto>> selectExpression,
      CancellationToken cancellationToken = default)
      where TEntity : class
   {
      var mapper = RequireMapper<TEntity>();
      model.OrderBy ??= mapper.GetDefaultOrderExpression();

      query = query.ApplyFilteringAndOrdering(model, mapper);

      var totalCount = await query.CountAsync(cancellationToken);

      var dtoQuery = query.Select(selectExpression)
                          .ApplyPaging(model.Page, model.PageSize);

      var data = await dtoQuery.ToListAsync(cancellationToken);
      return new PagedResponse<TDto>(data, model.Page, model.PageSize, totalCount);
   }

   public static Task<PagedResponse<TEntity>> FilterOrderAndGetPagedAsync<TEntity>(this IQueryable<TEntity> query,
      GridifyQueryModel model,
      CancellationToken cancellationToken = default)
      where TEntity : class =>
      query.AsNoTracking()
           .FilterOrderAndGetPagedAsync(model, x => x, cancellationToken);

   // ---------- Cursored ----------
   public static async Task<CursoredResponse<TDto>> FilterOrderAndGetCursoredAsync<TEntity, TDto>(
      this IQueryable<TEntity> query,
      GridifyCursoredQueryModel model,
      Expression<Func<TEntity, TDto>> selectExpression,
      CancellationToken cancellationToken = default)
      where TEntity : class
   {
      var mapper = RequireMapper<TEntity>();

      var queryModel = model.ToGridifyQueryModel();
      queryModel.OrderBy ??= mapper.GetDefaultOrderExpression();

      query = query.ApplyFilteringAndOrdering(queryModel, mapper);

      var data = await query.Select(selectExpression)
                            .Take(model.PageSize)
                            .ToListAsync(cancellationToken);

      return new CursoredResponse<TDto>(data, model.PageSize);
   }

   public static Task<CursoredResponse<TEntity>> FilterOrderAndGetCursoredAsync<TEntity>(this IQueryable<TEntity> query,
      GridifyCursoredQueryModel model,
      CancellationToken cancellationToken = default)
      where TEntity : class =>
      query.AsNoTracking()
           .FilterOrderAndGetCursoredAsync(model, x => x, cancellationToken);

   // ---------- Column Distinct ----------
   [Obsolete("Use ColumnDistinctValueCursoredQueryModel instead.")]
   public static async Task<PagedResponse<object>> ColumnDistinctValuesAsync<TEntity>(this IQueryable<TEntity> query,
      ColumnDistinctValueQueryModel model,
      Func<byte[], string>? decryptor = default,
      CancellationToken cancellationToken = default)
      where TEntity : class
   {
      var mapper = RequireMapper<TEntity>();

      if (!mapper.IsEncrypted(model.PropertyName))
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
                       .Select(CreateSelector<TEntity>(model.PropertyName))
                       .FirstOrDefaultAsync(cancellationToken);

      if (item is null || string.IsNullOrEmpty(model.Filter))
         return new PagedResponse<object>([], 1, 1, 0);

      if (decryptor is null)
      {
         throw new KeyNotFoundException("Decryptor is required for encrypted properties.");
      }

      var decrypted = decryptor((byte[])item);
      return new PagedResponse<object>([decrypted], 1, 1, 1);
   }

   public static async Task<CursoredResponse<object>> ColumnDistinctValuesAsync<TEntity>(this IQueryable<TEntity> query,
      ColumnDistinctValueCursoredQueryModel model,
      Func<byte[], string>? decryptor = default,
      CancellationToken cancellationToken = default)
      where TEntity : class
   {
      var mapper = RequireMapper<TEntity>();
      var gridifyModel = model.ToGridifyQueryModel();

      if (!mapper.IsEncrypted(model.PropertyName))
      {
         var result = await query
                            .ApplyFiltering(gridifyModel, mapper)
                            .ApplySelect(model.PropertyName, mapper)
                            .Distinct()
                            .OrderBy(x => x)
                            .Take(model.PageSize)
                            .ToListAsync(cancellationToken);

         return new CursoredResponse<object>(result, model.PageSize);
      }

      var item = await query
                       .ApplyFiltering(gridifyModel, mapper)
                       .Select(CreateSelector<TEntity>(model.PropertyName))
                       .FirstOrDefaultAsync(cancellationToken);

      if (item is null || string.IsNullOrEmpty(model.Filter))
         return new CursoredResponse<object>([], model.PageSize);

      if (decryptor is null)
      {
         throw new KeyNotFoundException("Decryptor is required for encrypted properties.");
      }

      var decrypted = decryptor((byte[])item);
      return new CursoredResponse<object>([decrypted], model.PageSize);
   }

   // ---------- Aggregation ----------
   public static async Task<object> AggregateAsync<TEntity>(this IQueryable<TEntity> query,
      AggregateQueryModel model,
      CancellationToken cancellationToken = default)
      where TEntity : class
   {
      var mapper = RequireMapper<TEntity>();
      var filtered = query.ApplyFiltering(model, mapper)
                          .ApplySelect(model.PropertyName, mapper);

      return model.AggregateType switch
      {
         AggregateType.UniqueCount => await filtered.Distinct()
                                                    .CountAsync(cancellationToken),
         AggregateType.Sum => await filtered.SumAsync(x => (decimal)x, cancellationToken),
         AggregateType.Average => await filtered.AverageAsync(x => (decimal)x, cancellationToken),
         AggregateType.Min => await filtered.MinAsync(cancellationToken),
         AggregateType.Max => await filtered.MaxAsync(cancellationToken),
         _ => throw new NotImplementedException()
      };
   }

   // ---------- Introspection ----------
   public static IEnumerable<MappingModel> GetMappings<TEntity>()
   {
      var mapper = EntityGridifyMapperByType[typeof(TEntity)] as FilterMapper<TEntity>;

      return mapper!.GetCurrentMaps()
                    .Select(x => new MappingModel
                    {
                       Name = x.From,
                       Type = x.To.Body is UnaryExpression ue
                          ? ue.Operand.Type.Name
                          : x.To.Body is MethodCallExpression mc
                             ? ((mc.Arguments.LastOrDefault() as LambdaExpression)?.ReturnType?.Name)
                               ?? x.To.Body.Type.Name
                             : x.To.Body.Type.Name
                    });
   }
}