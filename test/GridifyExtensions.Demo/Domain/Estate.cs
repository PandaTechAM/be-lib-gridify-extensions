namespace GridifyExtensions.Demo.Domain;

public class Estate
{
   public long Id { get; set; }
   public int Status { get; set; }

   public long BuildingId { get; set; }
   public Building Building { get; set; } = null!;

   public decimal Sqm { get; set; }
   public int? ResidentsQuantity { get; set; }          // nullable int
   public decimal? Balance { get; set; }                // nullable decimal

   public string? Comment { get; set; }                 // nullable string with null/" "/""
   public string NonNullText { get; set; } = "";        // non-nullable string
   
   public string? NumberText { get; set; }

   public DateTime CreatedAt { get; set; }
   public DateTime UpdatedAt { get; set; }

   public List<EstateOwnerAssignment> EstateOwnerAssignments { get; set; } = [];
   public List<Tag> Tags { get; set; } = [];
}