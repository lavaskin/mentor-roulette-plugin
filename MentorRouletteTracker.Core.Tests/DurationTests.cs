using MentorRouletteTracker.Core;

namespace MentorRouletteTracker.Core.Tests;

public class DurationTests
{
	[Theory]
	[InlineData(0, "0s")]
	[InlineData(30, "30s")]
	[InlineData(60, "1m")]
	[InlineData(2700, "45m")]
	[InlineData(3324, "55m 24s")]
	[InlineData(3599, "59m 59s")]
	[InlineData(3600, "1h")]
	[InlineData(5400, "1h 30m")]
	[InlineData(5459, "1h 30m")]
	[InlineData(7200, "2h")]
	[InlineData(3_600_000, "1,000h")]
	public void Format_WritesTimesLikeTheSite(int seconds, string expected)
	{
		Assert.Equal(expected, Duration.Format(seconds));
	}

	[Fact]
	public void Format_RefusesNegativeTimes()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => Duration.Format(-1));
	}
}
