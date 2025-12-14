namespace GridifyExtensions.Demo.Domain;

public class Building
{
   public long Id { get; set; }
   public int Status { get; set; }
   public long PartnerId { get; set; }
   public Partner Partner { get; set; } = null!;
   public string Address { get; set; } = "";
   public List<Estate> Estates { get; set; } = [];
}