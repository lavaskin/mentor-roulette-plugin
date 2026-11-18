namespace MentorRouletteTracker.Core.Api;

/// <summary>
/// The site's address, which a dev build's settings can change (to a local copy of the site, say)
/// </summary>
public static class ServerAddress
{
	public const string Default = "https://mentorroulette.com";

	/// <summary>
	/// Addresses the site had before <see cref="Default"/>. They still answer, so settings saved with one are for the
	/// same site, and their token still works.
	/// </summary>
	private static readonly string[] Former = ["https://mentor.lavask.in"];

	/// <summary>
	/// An http or https address, without a trailing slash, or null if <paramref name="text"/> isn't one
	/// </summary>
	public static string? Normalize(string text)
	{
		return Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
			&& (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
			&& string.IsNullOrEmpty(uri.Query)
			&& string.IsNullOrEmpty(uri.Fragment)
				? uri.GetLeftPart(UriPartial.Path).TrimEnd('/')
				: null;
	}

	/// <summary>
	/// A page on the site, like <c>Link(server, "roulettes?run=12")</c>
	/// </summary>
	public static string Link(string serverAddress, string pathAndQuery)
	{
		return serverAddress.TrimEnd('/') + "/" + pathAndQuery.TrimStart('/');
	}

	/// <summary>
	/// What loading the plugin does with the address in its saved settings. One of the site's former addresses moves
	/// to <see cref="Default"/>, staying connected. Any other address is a dev copy of the site: a dev build keeps it,
	/// and the installed plugin goes back to <see cref="Default"/> without the token, which was for that copy.
	/// </summary>
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
	/// <summary>Keep the saved address</summary>
	None,

	/// <summary>Switch to <see cref="ServerAddress.Default"/>, keeping the token and username</summary>
	Move,

	/// <summary>Switch to <see cref="ServerAddress.Default"/>, dropping the token and username</summary>
	Reset,
}
