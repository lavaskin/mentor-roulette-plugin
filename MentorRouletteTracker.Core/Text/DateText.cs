using System.Globalization;

namespace MentorRouletteTracker.Core.Text;

public static class DateText
{
	private static readonly CultureInfo UsEnglish = CultureInfo.GetCultureInfo("en-US");

	public static string Format(DateTime utc, TimeZoneInfo? zone = null)
	{
		var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone ?? TimeZoneInfo.Local);
		return local.ToString("MMM d, yyyy, h:mm tt", UsEnglish);
	}
}
