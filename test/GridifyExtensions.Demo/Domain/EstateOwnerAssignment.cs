namespace GridifyExtensions.Demo.Domain;

public class EstateOwnerAssignment
{
   public long Id { get; set; }

   public long EstateId { get; set; }
   public Estate Estate { get; set; } = null!;

   public long PartnerId { get; set; }
   public Partner Partner { get; set; } = null!;

   public bool IsPrimary { get; set; }
   public DateTime? EndDate { get; set; }
   public bool Deleted { get; set; }
}