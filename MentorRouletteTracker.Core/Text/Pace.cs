using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker.Core.Text;

/// <summary>
/// How a run's time compares with the usual, for the chat line: "About usual." or "Slow, 4m over the usual."
/// </summary>
public static class Pace
{
	private const int Minute = 60;

	public static string Format(RunPace pace)
	{
		var label = pace.Verdict switch
		{
			PaceVerdict.VeryQuick => "Very quick",
			PaceVerdict.Quick => "Quick",
			PaceVerdict.Slow => "Slow",
			PaceVerdict.VerySlow => "Very slow",
			_ => "About usual",
		};

		if (pace.Verdict == PaceVerdict.AboutUsual || pace.GapSeconds == 0)
		{
			return $"{label}.";
		}

		var side = pace.GapSeconds < 0 ? "under" : "over";
		return $"{label}, {Gap(Math.Abs(pace.GapSeconds))} {side} the usual.";
	}

	/// <summary>
	/// Under a minute in seconds ("12s"), and from a minute in whole minutes, rounded ("4m", "1h 5m"), since the usual is
	/// the median of a handful of runs and isn't exact to the second
	/// </summary>
	private static string Gap(int seconds)
	{
		if (seconds < Minute)
		{
			return Duration.Format(seconds);
		}

		var minutes = (int)Math.Round(seconds / (double)Minute, MidpointRounding.AwayFromZero);
		return Duration.Format(minutes * Minute);
	}
}
