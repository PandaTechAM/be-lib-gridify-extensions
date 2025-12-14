using GridifyExtensions.Demo.Domain;
using GridifyExtensions.Extensions;
using GridifyExtensions.Models;

namespace GridifyExtensions.Demo.Context;

public class EstateMapper : FilterMapper<Estate>
{
   public EstateMapper()
   {
      GenerateMappings();

      // Direct scalar
      AddMap("Id", x => x.Id);
      AddMap("Status", x => x.Status);
      AddMap("Sqm", x => x.Sqm);
      AddMap("ResidentsQuantity", x => x.ResidentsQuantity);
      AddMap("Balance", x => x.Balance);
      AddMap("CreatedAt", x => x.CreatedAt, x => x.ToUtcDateTime());

      // Strings with null/""/"   "
      AddMap("Comment", x => x.Comment);
      AddMap("NonNullText", x => x.NonNullText);
      AddMap("NumberText", x => x.NumberText);

      // Join-based scalar (ok)
      AddMap("BuildingAddress", x => x.Building.Address);

      // N:M example (collection) - STILL NOT SUPPORTED!
      AddMap("TagNames", x => x.Tags.Select(t => t.Name));

      // ✅ Critical: derived scalar (avoid collection map here)
      AddMap("PrimaryOwnerId",
         x => x.EstateOwnerAssignments
               .Where(a => a.IsPrimary && a.EndDate == null && !a.Deleted)
               .Max(a => (long?)a.PartnerId));

      AddMap("PrimaryOwnerFullName",
         x => x.EstateOwnerAssignments
               .Where(a => a.IsPrimary && a.EndDate == null && !a.Deleted)
               .Max(a => a.Partner.FullName));

      AddDefaultOrderByDescending("Id");
   }
}