using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Uploading;

namespace MentorRouletteTracker.Core.Tests;

/// <summary>
/// The site, answering each run by its duty: a run on a duty in <see cref="Answers"/> gets that answer, and any
/// other is logged with the next run number
/// </summary>
internal sealed class FakeApi : IMentorApi
{
	private int _nextRunNumber = 100;

	public Dictionary<uint, ApiResult<LoggedRun>> Answers { get; } = [];

	/// <summary>
	/// Every run sent, in order
	/// </summary>
	public List<Run> Sent { get; } = [];

	public List<ApiConnection> Connections { get; } = [];

	public Task<ApiResult<string>> GetUsernameAsync(ApiConnection connection, CancellationToken cancellationToken)
	{
		Connections.Add(connection);
		return Task.FromResult(ApiResult<string>.Ok("lavaskin"));
	}

	public Task<ApiResult<LoggedRun>> LogRunAsync(ApiConnection connection, Run run, CancellationToken cancellationToken)
	{
		Connections.Add(connection);
		Sent.Add(run);
		if (Answers.TryGetValue(run.GameContentId, out var answer))
		{
			return Task.FromResult(answer);
		}

		var number = _nextRunNumber++;
		return Task.FromResult(ApiResult<LoggedRun>.Ok(new LoggedRun(number + 5000, number)));
	}
}

internal sealed class FakeSettings : IUploadSettings
{
	public string ServerAddress { get; set; } = "https://mentor.example";
	public string? Token { get; set; } = "mrt_token";
	public bool AutoUpload { get; set; } = true;
}

internal static class Runs
{
	public static readonly DateTime Day = new(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);

	public static Run Make(int hour, uint duty = 421, RunStatus status = RunStatus.NotUploaded)
	{
		return new Run
		{
			Id = Guid.NewGuid(),
			GameContentId = duty,
			GameClassJobId = 19,
			Cleared = true,
			Replacement = false,
			Wipes = 0,
			DurationSeconds = 1200,
			DatePlayed = Day.AddHours(hour),
			Status = status,
		};
	}
}
