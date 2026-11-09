using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Storage;

namespace MentorRouletteTracker.Core.Uploading;

/// <summary>
/// Keeps each run that ends, and uploads it when auto-upload is on and the plugin's connected. Auto-upload sends
/// any older runs that aren't uploaded first.
/// </summary>
/// <remarks>
/// A run that just ended is told about in its own chat line, with what its upload found, even when another pass (like
/// "Upload N runs") is the one that uploads it. Every other run logged is one that was waiting, told about in
/// <see cref="WaitingRunsLogged"/>.
/// </remarks>
public sealed class RunKeeper
{
	private readonly RunLog _log;
	private readonly Uploader _uploader;
	private readonly IUploadSettings _settings;

	/// <summary>
	/// The runs that just ended whose outcome isn't decided yet, with the new duty each one's upload found. Locked.
	/// </summary>
	private readonly Dictionary<Guid, NewDuty?> _ending = [];

	public RunKeeper(RunLog log, Uploader uploader, IUploadSettings settings)
	{
		_log = log;
		_uploader = uploader;
		_settings = settings;
		_uploader.Logged += OnLogged;
	}

	/// <summary>
	/// Runs that were waiting were logged, by any upload, in the order they were logged. Not the runs that just ended,
	/// which <see cref="KeepAsync"/>'s outcome tells about. On the thread that uploaded them.
	/// </summary>
	public event Action<IReadOnlyList<LoggedRun>>? WaitingRunsLogged;

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
		lock (_ending)
		{
			_ending[run.Id] = null;
		}

		_log.Add(run);
		return Task.Run(() => UploadAsync(run, cancellationToken), cancellationToken);
	}

	private async Task<RunOutcome> UploadAsync(Run run, CancellationToken cancellationToken)
	{
		NewDuty? newDuty;
		try
		{
			if (AutoUploading)
			{
				await _uploader.UploadAsync(cancellationToken);
			}
		}
		finally
		{
			lock (_ending)
			{
				_ending.Remove(run.Id, out newDuty);
			}
		}

		// Uploaded comes first: with auto-upload off, "Upload N runs" may have sent it meanwhile
		var kept = _log.Find(run.Id) ?? run;
		if (kept.Status == RunStatus.Uploaded)
		{
			return new RunOutcome(RunOutcomeKind.Logged, RunNumber: kept.RunNumber, NewDuty: newDuty);
		}

		if (ConnectionFor(_settings) is null)
		{
			return new RunOutcome(RunOutcomeKind.NotConnected);
		}

		if (!_settings.AutoUpload)
		{
			return new RunOutcome(RunOutcomeKind.AutoUploadOff);
		}

		return kept.Status switch
		{
			RunStatus.Failed => new RunOutcome(RunOutcomeKind.Refused, Reason: kept.FailedReason),
			_ when _uploader.NeedsReconnect => new RunOutcome(RunOutcomeKind.NeedsReconnect),
			_ => new RunOutcome(RunOutcomeKind.Waiting),
		};
	}

	private void OnLogged(IReadOnlyList<(Guid RunId, LoggedRun Logged)> logged)
	{
		var waiting = new List<LoggedRun>();
		lock (_ending)
		{
			foreach (var (id, run) in logged)
			{
				if (_ending.ContainsKey(id))
				{
					_ending[id] = run.NewDuty;
				}
				else
				{
					waiting.Add(run);
				}
			}
		}

		if (waiting.Count > 0)
		{
			WaitingRunsLogged?.Invoke(waiting);
		}
	}

	/// <summary>
	/// When the plugin starts: uploads any runs left waiting, if auto-upload's on
	/// </summary>
	public Task ResumeAsync(CancellationToken cancellationToken = default)
	{
		return AutoUploading && _log.NotUploaded.Count > 0 ? _uploader.UploadAsync(cancellationToken) : Task.CompletedTask;
	}
}
