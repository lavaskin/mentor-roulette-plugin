using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Storage;

namespace MentorRouletteTracker.Core.Uploading;

/// <summary>
/// Keeps each run that ends, and uploads it when auto-upload is on and the plugin's connected. Auto-upload sends
/// any older runs that aren't uploaded first.
/// </summary>
public sealed class RunKeeper
{
	private readonly RunLog _log;
	private readonly Uploader _uploader;
	private readonly IUploadSettings _settings;

	public RunKeeper(RunLog log, Uploader uploader, IUploadSettings settings)
	{
		_log = log;
		_uploader = uploader;
		_settings = settings;
	}

	public static ApiConnection? ConnectionFor(IUploadSettings settings)
	{
		return string.IsNullOrEmpty(settings.Token) ? null : new ApiConnection(settings.ServerAddress, settings.Token);
	}

	/// <summary>
	/// Uploading runs by itself: connected, with auto-upload on, and the site still accepting the token
	/// </summary>
	public bool AutoUploading => ConnectionFor(_settings) is not null && _settings.AutoUpload && !_uploader.NeedsReconnect;

	/// <summary>
	/// Keeps a run that just ended, before returning, then uploads it off the calling thread if auto-upload's on
	/// </summary>
	public Task<RunOutcome> KeepAsync(Run run, CancellationToken cancellationToken = default)
	{
		_log.Add(run);
		return Task.Run(() => UploadAsync(run, cancellationToken), cancellationToken);
	}

	private async Task<RunOutcome> UploadAsync(Run run, CancellationToken cancellationToken)
	{
		if (ConnectionFor(_settings) is null)
		{
			return new RunOutcome(RunOutcomeKind.NotConnected);
		}

		if (!_settings.AutoUpload)
		{
			return new RunOutcome(RunOutcomeKind.AutoUploadOff);
		}

		if (!_uploader.NeedsReconnect)
		{
			await _uploader.UploadAsync(cancellationToken);
		}

		var kept = _log.Find(run.Id) ?? run;
		return kept.Status switch
		{
			RunStatus.Uploaded => new RunOutcome(RunOutcomeKind.Logged, RunNumber: kept.RunNumber),
			RunStatus.Failed => new RunOutcome(RunOutcomeKind.Refused, Reason: kept.FailedReason),
			_ when _uploader.NeedsReconnect => new RunOutcome(RunOutcomeKind.NeedsReconnect),
			_ => new RunOutcome(RunOutcomeKind.Waiting),
		};
	}

	/// <summary>
	/// When the plugin starts: uploads any runs left waiting, if auto-upload's on
	/// </summary>
	public Task ResumeAsync(CancellationToken cancellationToken = default)
	{
		return AutoUploading && _log.NotUploaded.Count > 0 ? _uploader.UploadAsync(cancellationToken) : Task.CompletedTask;
	}
}
