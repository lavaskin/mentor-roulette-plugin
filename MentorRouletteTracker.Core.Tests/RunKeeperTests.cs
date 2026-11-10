using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Storage;
using MentorRouletteTracker.Core.Uploading;

namespace MentorRouletteTracker.Core.Tests;

public class RunKeeperTests
{
	private readonly FakeApi _api = new();
	private readonly FakeSettings _settings = new();
	private readonly RunLog _log = new([]);
	private readonly Uploader _uploader;
	private readonly RunKeeper _keeper;

	public RunKeeperTests()
	{
		_uploader = new Uploader(_log, _api, () => RunKeeper.ConnectionFor(_settings));
		_keeper = new RunKeeper(_log, _uploader, _settings);
	}

	[Fact]
	public async Task WithAutoUploadOn_ARunUploadsWhenItEnds()
	{
		var run = Runs.Make(hour: 1);

		var outcome = await _keeper.KeepAsync(run);

		Assert.Equal(new RunOutcome(RunOutcomeKind.Logged, RunNumber: 100), outcome);
		Assert.Equal(RunStatus.Uploaded, _log.Find(run.Id)!.Status);
	}

	[Fact]
	public async Task WithAutoUploadOn_OlderRunsGoFirst()
	{
		var older = Runs.Make(hour: 1);
		_log.Add(older);
		var run = Runs.Make(hour: 2);

		var outcome = await _keeper.KeepAsync(run);

		Assert.Equal([older.Id, run.Id], _api.Sent.Select(sent => sent.Id));
		Assert.Equal(101, outcome.RunNumber);
	}

	[Fact]
	public async Task WithAutoUploadOff_ARunIsOnlySaved()
	{
		_settings.AutoUpload = false;
		var run = Runs.Make(hour: 1);

		var outcome = await _keeper.KeepAsync(run);

		Assert.Equal(RunOutcomeKind.AutoUploadOff, outcome.Kind);
		Assert.Empty(_api.Sent);
		Assert.Equal(RunStatus.NotUploaded, _log.Find(run.Id)!.Status);
	}

	[Fact]
	public async Task WhenNotConnected_ARunIsOnlySaved()
	{
		_settings.Token = null;

		var outcome = await _keeper.KeepAsync(Runs.Make(hour: 1));

		Assert.Equal(RunOutcomeKind.NotConnected, outcome.Kind);
		Assert.Empty(_api.Sent);
		Assert.Single(_log.Runs);
	}

	[Fact]
	public void ARun_IsKeptBeforeKeepReturns()
	{
		_settings.Token = null;
		var run = Runs.Make(hour: 1);

		_ = _keeper.KeepAsync(run);

		Assert.NotNull(_log.Find(run.Id));
	}

	[Fact]
	public async Task WhenTheSiteCantBeReached_TheRunWaits()
	{
		_api.Answers[421] = new ApiResult<LoggedRun>(ApiResultKind.Unavailable, Message: "Couldn't reach the site.");

		var outcome = await _keeper.KeepAsync(Runs.Make(hour: 1));

		Assert.Equal(RunOutcomeKind.Waiting, outcome.Kind);
	}

	[Fact]
	public async Task WhenTheSiteRefusesIt_TheOutcomeSaysWhy()
	{
		_api.Answers[421] = new ApiResult<LoggedRun>(ApiResultKind.Refused, Message: "That duty isn't in the Mentor Roulette.");

		var outcome = await _keeper.KeepAsync(Runs.Make(hour: 1));

		Assert.Equal(new RunOutcome(RunOutcomeKind.Refused, Reason: "That duty isn't in the Mentor Roulette."), outcome);
	}

	[Fact]
	public async Task AfterA401_AutoUploadPauses_UntilReconnected()
	{
		_api.Answers[421] = new ApiResult<LoggedRun>(ApiResultKind.Unauthorized);
		var first = await _keeper.KeepAsync(Runs.Make(hour: 1));
		Assert.Equal(RunOutcomeKind.NeedsReconnect, first.Kind);
		Assert.False(_keeper.AutoUploading);

		var second = await _keeper.KeepAsync(Runs.Make(hour: 2));

		Assert.Equal(RunOutcomeKind.NeedsReconnect, second.Kind);
		Assert.Single(_api.Sent);

		_api.Answers.Remove(421);
		_uploader.ConnectionChanged();
		Assert.True(_keeper.AutoUploading);
	}

	[Fact]
	public async Task OnStart_RunsLeftWaitingUpload_WhenAutoUploadIsOn()
	{
		_log.Add(Runs.Make(hour: 1));

		await _keeper.ResumeAsync();

		Assert.Single(_api.Sent);
	}

	[Fact]
	public async Task OnStart_NothingUploads_WhenAutoUploadIsOff()
	{
		_settings.AutoUpload = false;
		_log.Add(Runs.Make(hour: 1));

		await _keeper.ResumeAsync();

		Assert.Empty(_api.Sent);
	}

	[Fact]
	public async Task ARunOnANewDuty_SaysWhatItFound()
	{
		_api.Answers[421] = ApiResult<LoggedRun>.Ok(new LoggedRun(5100, 100, NewVault));

		var outcome = await _keeper.KeepAsync(Runs.Make(hour: 1));

		Assert.Equal(new RunOutcome(RunOutcomeKind.Logged, RunNumber: 100, NewDuty: NewVault), outcome);
	}

	[Fact]
	public async Task OlderRunsLoggedFirst_AreToldAboutApart_FromTheRunThatEnded()
	{
		var waiting = new List<IReadOnlyList<LoggedRun>>();
		_keeper.WaitingRunsLogged += waiting.Add;
		_log.Add(Runs.Make(hour: 1, duty: 7));
		_log.Add(Runs.Make(hour: 2));
		_api.Answers[7] = ApiResult<LoggedRun>.Ok(new LoggedRun(5099, 99, NewVault));

		var outcome = await _keeper.KeepAsync(Runs.Make(hour: 3));

		Assert.Equal(new RunOutcome(RunOutcomeKind.Logged, RunNumber: 101), outcome);
		Assert.Equal([new LoggedRun(5099, 99, NewVault), new LoggedRun(5100, 100)], Assert.Single(waiting));
	}

	[Fact]
	public async Task RunsLoggedOnStart_AreRunsThatWereWaiting()
	{
		var waiting = new List<IReadOnlyList<LoggedRun>>();
		_keeper.WaitingRunsLogged += waiting.Add;
		_log.Add(Runs.Make(hour: 1));

		await _keeper.ResumeAsync();

		Assert.Equal([new LoggedRun(5100, 100)], Assert.Single(waiting));
	}

	[Fact]
	public async Task ARunThatEndsWhileAnotherPassUploads_GetsItsOwnLine_EvenIfThatPassLogsIt()
	{
		var waiting = new List<IReadOnlyList<LoggedRun>>();
		_keeper.WaitingRunsLogged += waiting.Add;
		var older = Runs.Make(hour: 1);
		_log.Add(older);
		_api.Answers[7] = ApiResult<LoggedRun>.Ok(new LoggedRun(5101, 101, NewVault));
		var sending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_api.Hold = (sending, answer);

		var resume = _keeper.ResumeAsync();
		await sending.Task;
		var outcome = _keeper.KeepAsync(Runs.Make(hour: 2, duty: 7));
		answer.SetResult();
		await resume;

		Assert.Equal(new RunOutcome(RunOutcomeKind.Logged, RunNumber: 101, NewDuty: NewVault), await outcome);
		Assert.Equal(2, _api.Sent.Count);
		Assert.Equal([new LoggedRun(5100, 100)], Assert.Single(waiting));
	}

	[Fact]
	public async Task WithAutoUploadOff_ARunThatEndsWhileUploadingByHand_GetsItsOwnLine_WhenThatPassLogsIt()
	{
		_settings.AutoUpload = false;
		var waiting = new List<IReadOnlyList<LoggedRun>>();
		_keeper.WaitingRunsLogged += waiting.Add;
		_log.Add(Runs.Make(hour: 1));
		_api.Answers[7] = ApiResult<LoggedRun>.Ok(new LoggedRun(5101, 101, NewVault));
		var sending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_api.Hold = (sending, answer);

		var byHand = _uploader.UploadAsync();
		await sending.Task;
		var outcome = _keeper.KeepAsync(Runs.Make(hour: 2, duty: 7));
		answer.SetResult();
		await byHand;

		Assert.Equal(new RunOutcome(RunOutcomeKind.Logged, RunNumber: 101, NewDuty: NewVault), await outcome);
		Assert.Equal([new LoggedRun(5100, 100)], Assert.Single(waiting));
	}

	[Fact]
	public async Task WithAutoUploadOff_UploadingByHand_LogsRunsThatWereWaiting()
	{
		_settings.AutoUpload = false;
		var waiting = new List<IReadOnlyList<LoggedRun>>();
		_keeper.WaitingRunsLogged += waiting.Add;
		var outcome = await _keeper.KeepAsync(Runs.Make(hour: 1));

		await _uploader.UploadAsync();

		Assert.Equal(RunOutcomeKind.AutoUploadOff, outcome.Kind);
		Assert.Equal([new LoggedRun(5100, 100)], Assert.Single(waiting));
	}

	[Fact]
	public async Task ARetriedRun_IsARunThatWasWaiting()
	{
		var waiting = new List<IReadOnlyList<LoggedRun>>();
		_keeper.WaitingRunsLogged += waiting.Add;
		_api.Answers[421] = new ApiResult<LoggedRun>(ApiResultKind.Refused, Message: "No.");
		var run = Runs.Make(hour: 1);
		await _keeper.KeepAsync(run);
		_api.Answers.Remove(421);

		await _uploader.RetryAsync(run.Id);

		Assert.Equal([new LoggedRun(5100, 100)], Assert.Single(waiting));
	}

	private static readonly NewDuty NewVault = new(5, 10, new SeenTotals(3, 20), new SeenTotals(1, 4), "Dungeon", "3.0: Heavensward");
}
