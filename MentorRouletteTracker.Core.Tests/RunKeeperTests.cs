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
}
