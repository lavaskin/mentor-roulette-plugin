using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker.Core.Tests;

public class UploaderTests
{
	private const uint RefusedDuty = 1;
	private const uint DownDuty = 2;
	private const uint UnauthorizedDuty = 3;

	private readonly FakeApi _api = new();
	private readonly FakeSettings _settings = new();

	public UploaderTests()
	{
		_api.Answers[RefusedDuty] = new ApiResult<LoggedRun>(ApiResultKind.Refused, Message: "That duty isn't in the Mentor Roulette.");
		_api.Answers[DownDuty] = new ApiResult<LoggedRun>(ApiResultKind.Unavailable, Message: "Couldn't reach the site.");
		_api.Answers[UnauthorizedDuty] = new ApiResult<LoggedRun>(ApiResultKind.Unauthorized, Message: "The site didn't accept the token.");
	}

	private Uploader UploaderFor(RunLog log) => new(log, _api, () => RunKeeper.ConnectionFor(_settings));

	[Fact]
	public async Task Runs_UploadOldestFirst()
	{
		var newest = Runs.Make(hour: 3);
		var oldest = Runs.Make(hour: 1);
		var middle = Runs.Make(hour: 2);
		var log = new RunLog([newest, oldest, middle]);

		await UploaderFor(log).UploadAsync();

		Assert.Equal([oldest.Id, middle.Id, newest.Id], _api.Sent.Select(run => run.Id));
		Assert.All(log.Runs, run => Assert.Equal(RunStatus.Uploaded, run.Status));
		Assert.Equal(100, log.Find(oldest.Id)!.RunNumber);
		Assert.Equal(5100, log.Find(oldest.Id)!.SiteRunId);
		Assert.Equal(102, log.Find(newest.Id)!.RunNumber);
	}

	[Fact]
	public async Task AnUpload_SendsTheConnection()
	{
		var log = new RunLog([Runs.Make(hour: 1)]);

		await UploaderFor(log).UploadAsync();

		Assert.Equal(new ApiConnection("https://mentor.example", "mrt_token"), Assert.Single(_api.Connections));
	}

	[Fact]
	public async Task AWaitingRun_HoldsUpTheRunsAfterIt()
	{
		var before = Runs.Make(hour: 1);
		var waiting = Runs.Make(hour: 2, duty: DownDuty);
		var after = Runs.Make(hour: 3);
		var log = new RunLog([before, waiting, after]);
		var uploader = UploaderFor(log);

		await uploader.UploadAsync();

		Assert.Equal([before.Id, waiting.Id], _api.Sent.Select(run => run.Id));
		Assert.Equal(RunStatus.Uploaded, log.Find(before.Id)!.Status);
		Assert.Equal(RunStatus.NotUploaded, log.Find(waiting.Id)!.Status);
		Assert.Equal(RunStatus.NotUploaded, log.Find(after.Id)!.Status);
		Assert.Equal("Couldn't reach the site.", uploader.Problem);
		Assert.False(uploader.NeedsReconnect);
	}

	[Fact]
	public async Task AWaitingRun_IsTriedAgain_AndTheProblemClears()
	{
		var waiting = Runs.Make(hour: 1, duty: DownDuty);
		var log = new RunLog([waiting]);
		var uploader = UploaderFor(log);
		await uploader.UploadAsync();

		_api.Answers.Remove(DownDuty);
		await uploader.UploadAsync();

		Assert.Equal(RunStatus.Uploaded, log.Find(waiting.Id)!.Status);
		Assert.Null(uploader.Problem);
	}

	[Fact]
	public async Task ARefusedRun_IsFailed_AndDoesntHoldUpTheRunsAfterIt()
	{
		var refused = Runs.Make(hour: 1, duty: RefusedDuty);
		var after = Runs.Make(hour: 2);
		var log = new RunLog([refused, after]);

		await UploaderFor(log).UploadAsync();

		var failed = log.Find(refused.Id)!;
		Assert.Equal(RunStatus.Failed, failed.Status);
		Assert.Equal("That duty isn't in the Mentor Roulette.", failed.FailedReason);
		Assert.Equal(RunStatus.Uploaded, log.Find(after.Id)!.Status);
	}

	[Fact]
	public async Task AFailedRun_IsSetAside_UntilItsRetried()
	{
		var failed = Runs.Make(hour: 1, duty: RefusedDuty, status: RunStatus.Failed);
		var log = new RunLog([failed]);

		await UploaderFor(log).UploadAsync();

		Assert.Empty(_api.Sent);
	}

	[Fact]
	public async Task A401_StopsUploading_AndMarksNothingFailed()
	{
		var first = Runs.Make(hour: 1, duty: UnauthorizedDuty);
		var second = Runs.Make(hour: 2);
		var log = new RunLog([first, second]);
		var uploader = UploaderFor(log);

		await uploader.UploadAsync();

		Assert.True(uploader.NeedsReconnect);
		Assert.Single(_api.Sent);
		Assert.All(log.Runs, run => Assert.Equal(RunStatus.NotUploaded, run.Status));
	}

	[Fact]
	public async Task Reconnecting_ClearsTheNeedToReconnect()
	{
		var log = new RunLog([Runs.Make(hour: 1, duty: UnauthorizedDuty)]);
		var uploader = UploaderFor(log);
		await uploader.UploadAsync();

		uploader.ConnectionChanged();

		Assert.False(uploader.NeedsReconnect);
	}

	[Fact]
	public async Task ARetry_ReusesThePluginRunId()
	{
		var waiting = Runs.Make(hour: 1, duty: DownDuty);
		var log = new RunLog([waiting]);
		var uploader = UploaderFor(log);
		await uploader.UploadAsync();

		_api.Answers.Remove(DownDuty);
		await uploader.UploadAsync();

		Assert.Equal(2, _api.Sent.Count);
		Assert.All(_api.Sent, sent => Assert.Equal(waiting.Id, sent.Id));
	}

	[Fact]
	public async Task RetryingAFailedRun_SendsItAgain_WithTheSamePluginRunId()
	{
		var refused = Runs.Make(hour: 1, duty: RefusedDuty);
		var log = new RunLog([refused]);
		var uploader = UploaderFor(log);
		await uploader.UploadAsync();

		_api.Answers.Remove(RefusedDuty);
		await uploader.RetryAsync(refused.Id);

		var run = log.Find(refused.Id)!;
		Assert.Equal(RunStatus.Uploaded, run.Status);
		Assert.Null(run.FailedReason);
		Assert.Equal([refused.Id, refused.Id], _api.Sent.Select(sent => sent.Id));
	}

	[Fact]
	public async Task RetryingAFailedRun_SendsOlderWaitingRunsFirst()
	{
		var older = Runs.Make(hour: 1);
		var failed = Runs.Make(hour: 2, status: RunStatus.Failed);
		var log = new RunLog([older, failed]);

		await UploaderFor(log).RetryAsync(failed.Id);

		Assert.Equal([older.Id, failed.Id], _api.Sent.Select(run => run.Id));
	}

	[Fact]
	public async Task NothingUploads_WhenNotConnected()
	{
		_settings.Token = null;
		var log = new RunLog([Runs.Make(hour: 1)]);

		await UploaderFor(log).UploadAsync();

		Assert.Empty(_api.Sent);
	}

	[Fact]
	public async Task UploadedRuns_ArentSentAgain()
	{
		var log = new RunLog([Runs.Make(hour: 1, status: RunStatus.Uploaded)]);

		await UploaderFor(log).UploadAsync();

		Assert.Empty(_api.Sent);
	}
}
