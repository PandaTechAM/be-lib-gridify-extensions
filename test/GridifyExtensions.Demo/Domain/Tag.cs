namespace GridifyExtensions.Demo.Domain;

public class Tag
{
   public long Id { get; set; }
   public string Name { get; set; } = "";
   public List<Estate> Estates { get; set; } = [];
}