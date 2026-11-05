namespace MentorRouletteTracker.Core.Api;

/// <summary>
/// The site's API, as far as the plugin uses it: its two plugin endpoints, which take a plugin token
/// </summary>
public interface IMentorApi
{
	/// <summary>
	/// <c>GET /api/plugin/me</c>: whose account the token is for
	/// </summary>
	Task<ApiResult<string>> GetUsernameAsync(ApiConnection connection, CancellationToken cancellationToken);

	/// <summary>
	/// <c>POST /api/plugin/runs</c>: logs a run. Sending one that's already logged returns it again.
	/// </summary>
	Task<ApiResult<LoggedRun>> LogRunAsync(ApiConnection connection, Run run, CancellationToken cancellationToken);
}

/// <summary>
/// Where to send requests, and the plugin token to send with them
/// </summary>
/// <param name="ServerAddress">The site, like <c>https://mentor.lavask.in</c></param>
public sealed record ApiConnection(string ServerAddress, string Token);

/// <summary>
/// A run as the site logged it
/// </summary>
/// <param name="Id">Its id on the site, for its link</param>
/// <param name="RunNumber">Its number on the site</param>
public sealed record LoggedRun(int Id, int RunNumber);

public enum ApiResultKind
{
	Ok,

	/// <summary>
	/// A 401: the token is missing, unknown or revoked, or its account is disabled
	/// </summary>
	Unauthorized,

	/// <summary>
	/// A 400: the site refused the request, and says why
	/// </summary>
	Refused,

	/// <summary>
	/// The site couldn't be reached or couldn't answer (a timeout, a 5xx, a 503), so the request can be tried again later
	/// </summary>
	Unavailable,
}

public sealed record ApiResult<T>(ApiResultKind Kind, T? Value = default, string? Message = null)
{
	public static ApiResult<T> Ok(T value) => new(ApiResultKind.Ok, value);
}
