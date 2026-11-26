using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker.Core.Tests;

public class PaceTests
{
	[Theory]
	[InlineData(PaceVerdict.VeryQuick, -720, "Very quick, 12m under the usual.")]
	[InlineData(PaceVerdict.Quick, -180, "Quick, 3m under the usual.")]
	[InlineData(PaceVerdict.Slow, 240, "Slow, 4m over the usual.")]
	[InlineData(PaceVerdict.VerySlow, 720, "Very slow, 12m over the usual.")]
	public void AVerdictOffTheUsual_SaysHowFarAndWhichWay(PaceVerdict verdict, int gapSeconds, string expected)
	{
		Assert.Equal(expected, Pace.Format(new RunPace(verdict, gapSeconds)));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-35)]
	[InlineData(400)]
	public void AboutUsual_SaysNoGap(int gapSeconds)
	{
		Assert.Equal("About usual.", Pace.Format(new RunPace(PaceVerdict.AboutUsual, gapSeconds)));
	}

	[Theory]
	[InlineData(12, "12s")]
	[InlineData(59, "59s")]
	[InlineData(60, "1m")]
	[InlineData(89, "1m")]
	[InlineData(90, "2m")]
	[InlineData(3570, "1h")]
	[InlineData(3900, "1h 5m")]
	public void TheGap_IsInSecondsUnderAMinute_ThenInWholeMinutesRoundingHalvesUp(int gapSeconds, string gap)
	{
		Assert.Equal($"Slow, {gap} over the usual.", Pace.Format(new RunPace(PaceVerdict.Slow, gapSeconds)));
	}

	[Fact]
	public void NoGapAtAll_GivesJustTheVerdict()
	{
		Assert.Equal("Quick.", Pace.Format(new RunPace(PaceVerdict.Quick, 0)));
	}
}
