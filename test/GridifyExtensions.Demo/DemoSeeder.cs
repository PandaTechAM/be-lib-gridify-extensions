using GridifyExtensions.Demo.Context;
using GridifyExtensions.Demo.Domain;
using Microsoft.EntityFrameworkCore;

namespace GridifyExtensions.Demo;

public sealed class DemoSeeder(PostgresContext db)
{
   public async Task<object> RecreateAsync(int estateCount,
      int buildingCount,
      int partnerCount,
      int tagCount,
      CancellationToken ct)
   {
      await db.Database.ExecuteSqlRawAsync("""
                                              TRUNCATE TABLE estate_owner_assignments RESTART IDENTITY CASCADE;
                                              TRUNCATE TABLE estate_tags RESTART IDENTITY CASCADE;
                                              TRUNCATE TABLE estates RESTART IDENTITY CASCADE;
                                              TRUNCATE TABLE buildings RESTART IDENTITY CASCADE;
                                              TRUNCATE TABLE partners RESTART IDENTITY CASCADE;
                                              TRUNCATE TABLE tags RESTART IDENTITY CASCADE;
                                           """,
         ct);

      var rnd = new Random(123);

      var partners = Enumerable.Range(1, partnerCount)
                               .Select(i => new Partner
                               {
                                  Status = i % 20 == 0 ? 2 : 0,
                                  FullName = $"Partner {i:000000}"
                               })
                               .ToList();

      db.Partners.AddRange(partners);
      await db.SaveChangesAsync(ct);

      var buildings = Enumerable.Range(1, buildingCount)
                                .Select(i => new Building
                                {
                                   Status = i % 30 == 0 ? 1 : 0,
                                   PartnerId = partners[rnd.Next(partners.Count)].Id,
                                   Address = $"Street {rnd.Next(1, 500)} / B{i:000000}"
                                })
                                .ToList();

      db.Buildings.AddRange(buildings);
      await db.SaveChangesAsync(ct);

      var tagList = Enumerable.Range(1, tagCount)
                              .Select(i => new Tag
                              {
                                 Name = $"Tag{i:000}"
                              })
                              .ToList();
      db.Tags.AddRange(tagList);
      await db.SaveChangesAsync(ct);

      // estates in batches
      const int batch = 5_000;
      var now = DateTime.UtcNow;

      for (var offset = 0; offset < estateCount; offset += batch)
      {
         var take = Math.Min(batch, estateCount - offset);
         var estates = new List<Estate>(take);

         for (var i = 0; i < take; i++)
         {
            var idx = offset + i + 1;
            var commentMode = idx % 10;

            var comment = commentMode switch
            {
               0 => null, // NULL
               1 => "", // empty
               2 => "   ", // whitespace
               _ => $"Note {idx}"
            };

            var e = new Estate
            {
               Status = idx % 25 == 0 ? 1 : 0,
               BuildingId = buildings[rnd.Next(buildings.Count)].Id,
               Sqm = (decimal)(20 + rnd.NextDouble() * 180),
               ResidentsQuantity = idx % 7 == 0 ? null : rnd.Next(1, 8),
               Balance = idx % 9 == 0 ? null : (decimal)(rnd.NextDouble() * 10_000),
               Comment = comment,
               NonNullText = idx % 11 == 0 ? "" : $"Text {idx}",
               NumberText = MakeNumberText(rnd, idx),
               CreatedAt = now.AddDays(-rnd.Next(0, 365)),
               UpdatedAt = now
            };

            // random tags (0..3)
            var tCount = rnd.Next(0, 4);
            for (var t = 0; t < tCount; t++)
            {
               e.Tags.Add(tagList[rnd.Next(tagList.Count)]);
            }

            estates.Add(e);
         }

         db.Estates.AddRange(estates);
         await db.SaveChangesAsync(ct);

         // Primary owner assignment for ~80% estates (some missing => null exists)
         var eoa = new List<EstateOwnerAssignment>(take);
         foreach (var e in estates)
         {
            if (!(rnd.NextDouble() < 0.8))
            {
               continue;
            }

            var p = partners[rnd.Next(partners.Count)];
            eoa.Add(new EstateOwnerAssignment
            {
               EstateId = e.Id,
               PartnerId = p.Id,
               IsPrimary = true,
               EndDate = null,
               Deleted = false
            });
         }

         db.EstateOwnerAssignments.AddRange(eoa);
         await db.SaveChangesAsync(ct);
      }

      return new
      {
         Partners = partnerCount,
         Buildings = buildingCount,
         Estates = estateCount,
         Tags = tagCount
      };
   }

   private static string MakeNumberText(Random rnd, int idx)
   {
      // Fixed “must exist” cases to test search behavior
      return idx switch
      {
         3 => "3",
         4 => "33",
         5 => "1233",
         6 => "0329333",
         7 => "918327983213",
         _ => rnd.Next(0, 6) switch
         {
            0 => rnd.Next(0, 10)
                    .ToString(), // "3"
            1 => rnd.Next(10, 100)
                    .ToString(), // "33"
            2 => rnd.Next(1000, 10000)
                    .ToString(), // "1233"
            3 => rnd.Next(0, 1_000_000)
                    .ToString("D7"), // "0329333"
            4 => rnd.NextInt64(10_000_000_000, 999_999_999_999)
                    .ToString(), // "918327983213"
            _ => $"{rnd.Next(0, 999)}{rnd.Next(0, 9999):D4}" // mixed
         }
      };
   }
}