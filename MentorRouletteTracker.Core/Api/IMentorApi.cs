using MentorRouletteTracker.Core.Models;

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
/// <param name="NewDuty">Set when it's the user's first run on its duty. Never set when the site already had the run,
/// so a retried upload isn't announced twice.</param>
public sealed record LoggedRun(long Id, int RunNumber, NewDuty? NewDuty = null);

/// <summary>
/// The duties seen once a new duty is logged, with it counted, out of the duties in the roulette, as the site counts
/// them. The same again for the duty's type, and for that type in its expansion, which say whether the run finished
/// either.
/// </summary>
/// <param name="OfType">Of the duty's type, in every expansion. Null when the duty has no type.</param>
/// <param name="OfTypeInExpansion">Of the duty's type in its expansion. Null when the duty has no type or no
/// expansion.</param>
/// <param name="DutyType">The duty's type, as the site writes it: "Dungeon"</param>
/// <param name="Expansion">The duty's expansion, as the site writes it: "3.0: Heavensward"</param>
public sealed record NewDuty(
	int DutiesSeen,
	int DutiesInRoulette,
	SeenTotals? OfType,
	SeenTotals? OfTypeInExpansion,
	string? DutyType,
	string? Expansion);

public sealed record SeenTotals(int Seen, int Total)
{
	/// <summary>
	/// Every one of them seen. Never for none of none.
	/// </summary>
	public bool IsFinished => Total > 0 && Seen == Total;
}

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
