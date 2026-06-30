using GridifyExtensions.Extensions;

namespace GridifyExtensions.Tests;

public class StringExtensionsTests
{
   [Theory]
   [InlineData("")]
   [InlineData("   ")]
   public void ToUtcDateTime_EmptyOrWhitespace_ReturnsUtcMinValue_WithoutThrowing(string value)
   {
      var result = value.ToUtcDateTime();

      Assert.Equal(DateTime.MinValue, result);
      Assert.Equal(DateTimeKind.Utc, result.Kind);
   }

   [Fact]
   public void ToUtcDateTime_ValidIso8601_ReturnsUtcInstant()
   {
      var result = "2026-06-30T08:23:48.900Z".ToUtcDateTime();

      Assert.Equal(new DateTime(2026, 6, 30, 8, 23, 48, 900, DateTimeKind.Utc), result);
      Assert.Equal(DateTimeKind.Utc, result.Kind);
   }
}
