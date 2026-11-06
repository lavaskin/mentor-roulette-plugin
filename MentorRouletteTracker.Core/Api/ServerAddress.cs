namespace MentorRouletteTracker.Core.Api;

/// <summary>
/// The site's address, which the settings let the user change (to the dev server, say)
/// </summary>
public static class ServerAddress
{
	public const string Default = "https://mentor.lavask.in";

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
}
