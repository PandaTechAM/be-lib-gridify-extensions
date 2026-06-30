namespace GridifyExtensions.Extensions;

public static class StringExtensions
{
   public static DateTime ToUtcDateTime(this string date)
   {
      // An empty value is sent for an open range bound (e.g. "CreatedAt>="). Mirror the built-in
      // DateTime TypeConverter (empty => MinValue) so the bound becomes a no-op instead of throwing
      // a FormatException that escapes as a 500 (Gridify does not guard custom converters).
      if (string.IsNullOrWhiteSpace(date))
      {
         return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
      }

      return DateTime.Parse(date)
                     .ToUniversalTime();
   }
}