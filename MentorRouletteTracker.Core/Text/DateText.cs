using System.Globalization;

namespace MentorRouletteTracker.Core.Text;

/// <summary>
/// Dates written like the site writes a day and its time: "Nov 5, 2026, 3:12 AM"
/// </summary>
public static class DateText
{
	private static readonly CultureInfo UsEnglish = CultureInfo.GetCultureInfo("en-US");

	/// <param name="utc">A time in UTC, shown in <paramref name="zone"/> (the PC's time zone, by default)</param>
	public static string Format(DateTime utc, TimeZoneInfo? zone = null)
	{
		var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone ?? TimeZoneInfo.Local);
		return local.ToString("MMM d, yyyy, h:mm tt", UsEnglish);
	}
}
