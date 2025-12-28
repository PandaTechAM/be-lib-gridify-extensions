using GridifyExtensions.Demo.Context;
using GridifyExtensions.Extensions;
using GridifyExtensions.Models;

namespace GridifyExtensions.Demo;

public static class Endpoints
{
   public static WebApplication MapEstateEndpoints(this WebApplication app)
   {
      var g = app.MapGroup("/estates");

      g.MapGet("/paged",
         async (PostgresContext db, [AsParameters] GridifyQueryModel request, CancellationToken ct) =>
         {
            var response = await db.Estates
                                   .FilterOrderAndGetPagedAsync(request,
                                      e => new
                                      {
                                         e.Id,
                                         e.Status,
                                         e.Sqm,
                                         e.ResidentsQuantity,
                                         e.Balance,
                                         e.Comment,
                                         e.NonNullText,
                                         BuildingAddress = e.Building.Address,
                                         PrimaryOwnerId = e.EstateOwnerAssignments
                                                           .Where(a => a.IsPrimary && a.EndDate == null && !a.Deleted)
                                                           .Select(a => (long?)a.PartnerId)
                                                           .FirstOrDefault(),
                                         PrimaryOwnerFullName = e.EstateOwnerAssignments
                                                                 .Where(a =>
                                                                    a.IsPrimary && a.EndDate == null && !a.Deleted)
                                                                 .Select(a => a.Partner.FullName)
                                                                 .FirstOrDefault()
                                      },
                                      ct);

            return Results.Ok(response);
         });

      g.MapGet("/distinct",
         async (PostgresContext db,
            [AsParameters] ColumnDistinctValueCursoredQueryModel request,
            CancellationToken ct) =>
         {
            var response = await db.Estates
                                   .ColumnDistinctValuesAsync(request, ct: ct);

            return Results.Ok(response);
         });

      app.MapPost("/seed",
         async (PostgresContext db, int? estates, int? buildings, int? partners, int? tags, CancellationToken ct) =>
         {
            var seed = new DemoSeeder(db);
            var res = await seed.RecreateAsync(
               estates ?? 100_000,
               buildings ?? 10_000,
               partners ?? 1_000,
               tags ?? 200,
               ct);

            return Results.Ok(res);
         });

      return app;
   }
}