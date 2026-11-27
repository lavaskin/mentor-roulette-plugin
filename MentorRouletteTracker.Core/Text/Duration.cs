using System.Globalization;

namespace MentorRouletteTracker.Core.Text;

public static class Duration
{
	private const int Minute = 60;
	private const int Hour = 60 * Minute;

	public static string Format(int seconds)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(seconds);

		if (seconds >= Hour)
		{
			return JoinParts((seconds / Hour, "h"), (seconds % Hour / Minute, "m"));
		}

		var text = JoinParts((seconds / Minute, "m"), (seconds % Minute, "s"));
		return text.Length > 0 ? text : "0s";
	}

	private static string JoinParts(params (int Count, string Unit)[] parts)
	{
		return string.Join(' ', parts
			.Where(part => part.Count > 0)
			.Select(part => part.Count.ToString("N0", CultureInfo.InvariantCulture) + part.Unit));
	}
}
