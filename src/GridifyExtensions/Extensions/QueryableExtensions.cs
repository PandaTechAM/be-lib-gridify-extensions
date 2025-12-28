using System.Collections;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Gridify;
using GridifyExtensions.Enums;
using GridifyExtensions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

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
      where TEntity : class
   {
      return query.ApplyFiltering(model, RequireMapper<TEntity>());
   }

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
      CancellationToken ct = default)
      where TEntity : class
   {
      var totalCount = await query.CountAsync(ct);
      query = query.ApplyPaging(model.Page, model.PageSize);
      var data = await query.ToListAsync(ct);
      return new PagedResponse<TEntity>(data, model.Page, model.PageSize, totalCount);
   }

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

   // ---------- Filter + Order + Paging ----------
   public static async Task<PagedResponse<TDto>> FilterOrderAndGetPagedAsync<TEntity, TDto>(
      this IQueryable<TEntity> query,
      GridifyQueryModel model,
      Expression<Func<TEntity, TDto>> selectExpression,
      CancellationToken ct = default)
      where TEntity : class
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

   public static Task<PagedResponse<TEntity>> FilterOrderAndGetPagedAsync<TEntity>(this IQueryable<TEntity> query,
      GridifyQueryModel model,
      CancellationToken ct = default)
      where TEntity : class
   {
      return query.AsNoTracking()
                  .FilterOrderAndGetPagedAsync(model, x => x, ct);
   }

   // ---------- Cursored ----------
   public static async Task<CursoredResponse<TDto>> FilterOrderAndGetCursoredAsync<TEntity, TDto>(
      this IQueryable<TEntity> query,
      GridifyCursoredQueryModel model,
      Expression<Func<TEntity, TDto>> selectExpression,
      CancellationToken ct = default)
      where TEntity : class
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

   public static Task<CursoredResponse<TEntity>> FilterOrderAndGetCursoredAsync<TEntity>(this IQueryable<TEntity> query,
      GridifyCursoredQueryModel model,
      CancellationToken ct = default)
      where TEntity : class
   {
      return query.AsNoTracking()
                  .FilterOrderAndGetCursoredAsync(model, x => x, ct);
   }

   // ---------- Column Distinct ----------
   [Obsolete("Use ColumnDistinctValueCursoredQueryModel instead.")]
   public static async Task<PagedResponse<object>> ColumnDistinctValuesAsync<TEntity>(this IQueryable<TEntity> query,
      ColumnDistinctValueQueryModel model,
      Func<byte[], string>? decryptor = null,
      CancellationToken ct = default)
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
                            .GetPagedAsync(model, ct);
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
            hasNullLike = await encryptedQuery.AnyAsync(x => x == null, ct);
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

      var selected = await encryptedQuery.FirstOrDefaultAsync(ct);
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

      var ng = ((IEnumerable)seq).GetEnumerator();
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
      CancellationToken ct = default)
      where TEntity : class
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
                           .ApplySelect(model.PropertyName, mapper); // IQueryable<object?>

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

      var selected = await encryptedQuery.FirstOrDefaultAsync(ct);
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

   // ---------- Aggregation ----------
   public static async Task<object> AggregateAsync<TEntity>(this IQueryable<TEntity> query,
      AggregateQueryModel model,
      CancellationToken ct = default)
      where TEntity : class
   {
      var mapper = RequireMapper<TEntity>();
      var filtered = query.ApplyFiltering(model, mapper)
                          .ApplySelect(model.PropertyName, mapper);

      return model.AggregateType switch
      {
         AggregateType.UniqueCount => await filtered.Distinct()
                                                    .CountAsync(ct),
         AggregateType.Sum => await filtered.SumAsync(x => (decimal)x, ct),
         AggregateType.Average => await filtered.AverageAsync(x => (decimal)x, ct),
         AggregateType.Min => await filtered.MinAsync(ct),
         AggregateType.Max => await filtered.MaxAsync(ct),
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


   private static Expression<Func<TEntity, string?>> EfStringSelector<TEntity>(string propertyName)
      where TEntity : class
   {
      var e = Expression.Parameter(typeof(TEntity), "e");
      var body = Expression.Call(
         typeof(EF),
         nameof(EF.Property),
         [
            typeof(string)
         ],
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
}