using Microsoft.EntityFrameworkCore;

namespace GridifyExtensions.DbContextFunction;

[Obsolete("After migration to Pandatech.Crypto 5, this class is not needed anymore.")]
public class
   PostgresFunctions : DbContext //By inheriting PostgresFunctions you can use the Substr function in your queries
{
   protected PostgresFunctions(DbContextOptions options) : base(options)
   {
   }

   [DbFunction("substr", IsBuiltIn = true)]
   public static byte[] Substr(byte[] target, int start, int count)
   {
      throw new NotImplementedException();
   }
}