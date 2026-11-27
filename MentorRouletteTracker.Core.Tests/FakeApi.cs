using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Tests;

internal sealed class FakeApi : IMentorApi
{
	private int _nextRunNumber = 100;

	public Dictionary<uint, ApiResult<LoggedRun>> Answers { get; } = [];

	public List<Run> Sent { get; } = [];

	public List<ApiConnection> Connections { get; } = [];

	public Action? OnSend { get; set; }

	public (TaskCompletionSource Sending, TaskCompletionSource Answer)? Hold { get; set; }

	public Task<ApiResult<string>> GetUsernameAsync(ApiConnection connection, CancellationToken cancellationToken)
	{
		Connections.Add(connection);
		return Task.FromResult(ApiResult<string>.Ok("example_user"));
	}

	public async Task<ApiResult<LoggedRun>> LogRunAsync(ApiConnection connection, Run run, CancellationToken cancellationToken)
	{
		Connections.Add(connection);
		Sent.Add(run);
		OnSend?.Invoke();

		if (Hold is { } hold)
		{
			Hold = null;
			hold.Sending.SetResult();
			await hold.Answer.Task;
		}

		if (Answers.TryGetValue(run.GameContentId, out var answer))
		{
			return answer;
		}

		var number = _nextRunNumber++;
		return ApiResult<LoggedRun>.Ok(new LoggedRun(number + 5000, number));
	}
}

internal static class ConnectedSettings
{
	public static Settings Make()
	{
		return new Settings
		{
			ServerAddress = "https://mentor.example",
			Token = "mrt_token",
		};
	}
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
