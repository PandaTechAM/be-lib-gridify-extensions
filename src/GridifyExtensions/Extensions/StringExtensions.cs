namespace GridifyExtensions.Extensions;

/// <summary>
///     String helpers used by Gridify custom converters.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    ///     Parse a date string to a UTC DateTime. An empty or whitespace value returns UTC DateTime.MinValue
    ///     (matching the built-in DateTime converter) so an open range bound is a no-op instead of throwing.
    /// </summary>
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
