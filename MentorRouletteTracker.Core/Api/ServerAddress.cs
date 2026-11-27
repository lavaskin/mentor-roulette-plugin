namespace MentorRouletteTracker.Core.Api;

public static class ServerAddress
{
	public const string Default = "https://mentorroulette.com";

	private static readonly string[] Former = ["https://mentor.lavask.in"];

	public static string? Normalize(string text)
	{
		return Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
			&& (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
			&& string.IsNullOrEmpty(uri.Query)
			&& string.IsNullOrEmpty(uri.Fragment)
				? uri.GetLeftPart(UriPartial.Path).TrimEnd('/')
				: null;
	}

	public static string Link(string serverAddress, string pathAndQuery)
	{
		return serverAddress.TrimEnd('/') + "/" + pathAndQuery.TrimStart('/');
	}

	public static SavedAddressChange OnLoad(string saved, bool isDev)
	{
		if (Normalize(saved) is { } address && Former.Contains(address, StringComparer.OrdinalIgnoreCase))
		{
			return SavedAddressChange.Move;
		}

		return isDev || saved == Default ? SavedAddressChange.None : SavedAddressChange.Reset;
	}
}

public enum SavedAddressChange
{
	None,

	Move,

	Reset,
}
