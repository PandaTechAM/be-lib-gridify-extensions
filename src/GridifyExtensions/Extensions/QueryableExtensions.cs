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
      Func<byte[], string>? decryptor = null,
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

      var encryptedQuery = query
                           .ApplyFiltering(model, mapper)
                           .ApplySelect(model.PropertyName, mapper); // IQueryable<object?>

      if (string.IsNullOrWhiteSpace(model.Filter))
      {
         bool hasNullLike;
         try
         {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            hasNullLike = await encryptedQuery.AnyAsync(x => x == null, cancellationToken);
         }
         catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
         {
            // NOTE:
            // Some providers cannot translate `Any(x => x == null)` when the projection is a COLLECTION
            // (e.g., IEnumerable<byte[]> coming from a nav). We need to decide what to do without
            // issuing a second, provider-specific query here.
            //
            // UX policy: when the frontend opens distinct-values with NO filter on an encrypted column,
            // we prefer to SHOW the "null" option rather than hide it due to translation limits.
            // Therefore we *assume* null-like exists. If you prefer strictness, set `hasNullLike = false`
            hasNullLike = true;
         }

         return hasNullLike ? new PagedResponse<object>([null!], 1, 1, 1) : new PagedResponse<object>([], 1, 1, 0);
      }

      var selected = await encryptedQuery.FirstOrDefaultAsync(cancellationToken);
      switch (selected)
      {
         case null:
         case byte[]
         {
            Length: 0
         }:
            return new PagedResponse<object>([null!], 1, 1, 1);
         case byte[] sb:
            return decryptor == null
               ? throw new KeyNotFoundException("Decryptor is required for encrypted properties.")
               : new PagedResponse<object>([decryptor(sb)], 1, 1, 1);
      }

      if (selected is not IEnumerable<byte[]> seq)
      {
         throw new InvalidCastException("Encrypted selector did not return a byte[] or IEnumerable<byte[]> value.");
      }

      var ng = ((System.Collections.IEnumerable)seq).GetEnumerator();
      using var ng1 = ng as IDisposable;

      if (!ng.MoveNext())
      {
         return new PagedResponse<object>([null!], 1, 1, 1);
      }

      var firstObj = ng.Current;
      if (firstObj is not byte[] first || first.Length == 0)
      {
         return new PagedResponse<object>([null!], 1, 1, 1);
      }

      return decryptor == null
         ? throw new KeyNotFoundException("Decryptor is required for encrypted properties.")
         : new PagedResponse<object>([decryptor(first)], 1, 1, 1);
   }

   public static async Task<CursoredResponse<object?>> ColumnDistinctValuesAsync<TEntity>(
      this IQueryable<TEntity> query,
      ColumnDistinctValueCursoredQueryModel model,
      Func<byte[], string>? decryptor = null,
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
            if (hasNull && take > 0)
            {
               take -= 1;
            }
         }

         // smart ordering for string values ---
         var orderedQuery = baseQuery.Distinct();

         if (typeof(string).IsAssignableFrom(orderedQuery.ElementType))
         {
            var stringQuery = (IQueryable<string?>)orderedQuery;

            orderedQuery = stringQuery
                           .OrderBy(x => x == null ? int.MaxValue : x.Length) // shorter first
                           .ThenBy(x => x)!; // then lexicographic
         }
         else
         {
            orderedQuery = orderedQuery.OrderBy(x => x);
         }

         var result = await orderedQuery
                            .Take(take)
                            .ToListAsync(cancellationToken);

         if (!filterEmpty || !hasNull)
         {
            return new CursoredResponse<object?>(result!, model.PageSize);
         }

         if (result.Count > 0 && ReferenceEquals(result[^1], null))
         {
            result.RemoveAt(result.Count - 1);
         }

         result.Insert(0, null!);
         return new CursoredResponse<object?>(result!, model.PageSize);
      }

      // Encrypted path
      var encryptedQuery = query
                           .ApplyFiltering(gridifyModel, mapper)
                           .ApplySelect(model.PropertyName, mapper); // IQueryable<object?>

      if (string.IsNullOrWhiteSpace(model.Filter))
      {
         bool hasNullLike;
         try
         {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            hasNullLike = await encryptedQuery.AnyAsync(x => x == null, cancellationToken);
         }
         catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
         {
            // NOTE:
            // Some providers cannot translate `Any(x => x == null)` when the projection is a COLLECTION
            // (e.g., IEnumerable<byte[]> coming from a nav). We need to decide what to do without
            // issuing a second, provider-specific query here.
            //
            // UX policy: when the frontend opens distinct-values with NO filter on an encrypted column,
            // we prefer to SHOW the "null" option rather than hide it due to translation limits.
            // Therefore we *assume* null-like exists. If you prefer strictness, set `hasNullLike = false`
            hasNullLike = true;
         }

         return hasNullLike
            ? new CursoredResponse<object?>([null], model.PageSize)
            : new CursoredResponse<object?>([], model.PageSize);
      }

      var selected = await encryptedQuery.FirstOrDefaultAsync(cancellationToken);
      switch (selected)
      {
         case null:
         case byte[]
         {
            Length: 0
         }:
            return new CursoredResponse<object?>([null], model.PageSize);
         case byte[] when decryptor == null:
            throw new KeyNotFoundException("Decryptor is required for encrypted properties.");
         case byte[] sb:
            return new CursoredResponse<object?>([decryptor(sb)], model.PageSize);
      }

      if (selected is not IEnumerable<byte[]> seq)
      {
         throw new InvalidCastException("Encrypted selector did not return a byte[] or IEnumerable<byte[]> value.");
      }

      var ng = ((System.Collections.IEnumerable)seq).GetEnumerator();
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
                       Type = x.To.Body switch
                       {
                          UnaryExpression ue => ue.Operand.Type.Name,
                          MethodCallExpression mc => (mc.Arguments.LastOrDefault() as LambdaExpression)?.ReturnType.Name
                                                     ?? x.To.Body.Type.Name,
                          _ => x.To.Body.Type.Name
                       }
                    });
   }
}