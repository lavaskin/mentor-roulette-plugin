using System.Globalization;

namespace MentorRouletteTracker.Core;

/// <summary>
/// Times written the way the site writes them (its <c>formatDuration</c>), so a run reads the same in the plugin
/// and on the site.
/// </summary>
public static class Duration
{
	private const int Minute = 60;
	private const int Hour = 60 * Minute;

	/// <summary>
	/// e.g. "1h 30m", "55m 24s", "45m", "30s". Zero parts are left out, and from an hour so are the seconds.
	/// </summary>
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
