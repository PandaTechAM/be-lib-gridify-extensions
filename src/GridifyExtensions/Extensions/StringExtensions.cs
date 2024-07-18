namespace GridifyExtensions.Extensions;

public static class StringExtensions
{
   public static DateTime ToUtcDateTime(this string date)
   {
      return DateTime.Parse(date)
                     .ToUniversalTime();
   }
}