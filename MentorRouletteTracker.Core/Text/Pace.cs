using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker.Core.Text;

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
