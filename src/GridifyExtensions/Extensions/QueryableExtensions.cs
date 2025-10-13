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

      var filtered = query.ApplyFiltering(model, mapper);

      var totalCount = await filtered.CountAsync(cancellationToken);

      var ordered = filtered.ApplyOrdering(model, mapper);

      var data = await ordered
                       .Select(selectExpression)
                       .ApplyPaging(model.Page, model.PageSize)
                       .ToListAsync(cancellationToken);

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

      // Encrypted path (scalar byte[] or IEnumerable<byte[]>), use mapper-driven selection
      var encryptedQuery = query
                           .ApplyFiltering(model, mapper)
                           .ApplySelect(model.PropertyName, mapper); // IQueryable<object?>

      // Keep original behavior: if no filter, return empty for the obsolete API
      if (string.IsNullOrWhiteSpace(model.Filter))
      {
         return new PagedResponse<object>([], 1, 1, 0);
      }

      var selected = await encryptedQuery.FirstOrDefaultAsync(cancellationToken);
      if (selected is null) return new PagedResponse<object>([], 1, 1, 0);
      if (decryptor is null) throw new KeyNotFoundException("Decryptor is required for encrypted properties.");

      object? decrypted = selected switch
      {
         byte[] b => decryptor(b),
         IEnumerable<byte[]> bs => bs.FirstOrDefault() is byte[] fb ? decryptor(fb) : null,
         _ => throw new InvalidCastException("Encrypted selector did not return a byte[] or IEnumerable<byte[]> value.")
      };

      return decrypted is null
         ? new PagedResponse<object>([], 1, 1, 0)
         : new PagedResponse<object>([decrypted], 1, 1, 1);
   }

   public static async Task<CursoredResponse<object?>> ColumnDistinctValuesAsync<TEntity>(
      this IQueryable<TEntity> query,
      ColumnDistinctValueCursoredQueryModel model,
      Func<byte[], string>? decryptor = default,
      CancellationToken cancellationToken = default)
      where TEntity : class
   {
      var mapper = RequireMapper<TEntity>();
      var gridifyModel = model.ToGridifyQueryModel();

      if (!mapper.IsEncrypted(model.PropertyName))
      {
         var baseQuery = query
                         .ApplyFiltering(gridifyModel, mapper)
                         .ApplySelect(model.PropertyName, mapper);

         var filterEmpty = string.IsNullOrWhiteSpace(gridifyModel.Filter);
         var hasNull = false;
         var take = model.PageSize;

         if (filterEmpty)
         {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            hasNull = await baseQuery.AnyAsync(x => x == null, cancellationToken);
            if (hasNull && take > 0) take -= 1;
         }

         var result = await baseQuery
                            .Distinct()
                            .OrderBy(x => x)
                            .Take(take)
                            .ToListAsync(cancellationToken);

         if (!filterEmpty || !hasNull)
            return new CursoredResponse<object?>(result!, model.PageSize);

         if (result.Count > 0 && ReferenceEquals(result[^1], null))
            result.RemoveAt(result.Count - 1);

         result.Insert(0, null!);
         return new CursoredResponse<object?>(result!, model.PageSize);
      }

      // Encrypted path (scalar byte[] or IEnumerable<byte[]>)
      var encryptedQuery = query
                           .ApplyFiltering(gridifyModel, mapper)
                           .ApplySelect(model.PropertyName, mapper); // IQueryable<object?>

      if (string.IsNullOrWhiteSpace(model.Filter))
      {
         // EF-translatable: only checks if the projection itself is NULL in DB
         // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
         var hasNull = await encryptedQuery.AnyAsync(x => x == null, cancellationToken);

         return hasNull
            ? new CursoredResponse<object?>([null], model.PageSize)
            : new CursoredResponse<object?>([], model.PageSize);
      }

      var selected = await encryptedQuery.FirstOrDefaultAsync(cancellationToken);
      if (selected is null)
      {
         return new CursoredResponse<object?>([], model.PageSize);
      }

      if (decryptor is null)
      {
         throw new KeyNotFoundException("Decryptor is required for encrypted properties.");
      }

      object? decrypted = selected switch
      {
         byte[] b => decryptor(b),
         IEnumerable<byte[]> bs => bs.FirstOrDefault() is
            { } fb
            ? decryptor(fb)
            : null,
         _ => throw new InvalidCastException("Encrypted selector did not return a byte[] or IEnumerable<byte[]> value.")
      };

      return decrypted is null
         ? new CursoredResponse<object?>([], model.PageSize)
         : new CursoredResponse<object?>([decrypted], model.PageSize);
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