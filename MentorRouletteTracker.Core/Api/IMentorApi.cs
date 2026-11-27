using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Api;

public interface IMentorApi
{
	Task<ApiResult<string>> GetUsernameAsync(ApiConnection connection, CancellationToken cancellationToken);

	Task<ApiResult<LoggedRun>> LogRunAsync(ApiConnection connection, Run run, CancellationToken cancellationToken);
}

public sealed record ApiConnection(string ServerAddress, string Token);

public sealed record LoggedRun(long Id, int RunNumber, NewDuty? NewDuty = null, RunPace? Pace = null);

public sealed record RunPace(PaceVerdict Verdict, int GapSeconds);

public enum PaceVerdict
{
	VeryQuick = 1,
	Quick = 2,
	AboutUsual = 3,
	Slow = 4,
	VerySlow = 5,
}

public sealed record NewDuty(
	int DutiesSeen,
	int DutiesInRoulette,
	SeenTotals? OfType,
	SeenTotals? OfTypeInExpansion,
	string? DutyType,
	string? Expansion);

public sealed record SeenTotals(int Seen, int Total)
{
	public bool IsFinished => Total > 0 && Seen == Total;
}

public enum ApiResultKind
{
	Ok,

	Unauthorized,

	Refused,

	Unavailable,
}

public sealed record ApiResult<T>(ApiResultKind Kind, T? Value = default, string? Message = null)
{
	public static ApiResult<T> Ok(T value) => new(ApiResultKind.Ok, value);
}
