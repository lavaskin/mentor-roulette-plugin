using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Storage;

namespace MentorRouletteTracker.Core.Uploading;

public sealed class RunKeeper
{
	private readonly RunLog _log;
	private readonly Uploader _uploader;
	private readonly Settings _settings;

	private readonly Dictionary<Guid, LoggedRun?> _ending = [];

	public RunKeeper(RunLog log, Uploader uploader, Settings settings)
	{
		_log = log;
		_uploader = uploader;
		_settings = settings;
		_uploader.Logged += OnLogged;
	}

	public event Action<IReadOnlyList<LoggedRun>>? WaitingRunsLogged;

	public static ApiConnection? ConnectionFor(Settings settings)
	{
		return string.IsNullOrEmpty(settings.Token) ? null : new ApiConnection(settings.ServerAddress, settings.Token);
	}

	public bool AutoUploading => ConnectionFor(_settings) is not null && _settings.AutoUpload && !_uploader.NeedsReconnect;

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
		LoggedRun? logged;
		try
		{
			if (AutoUploading)
			{
				await _uploader.UploadAsync(cancellationToken);
			}
			else
			{
				await _uploader.WaitForPassAsync(cancellationToken);
			}
		}
		finally
		{
			lock (_ending)
			{
				_ending.Remove(run.Id, out logged);
			}
		}

		if (logged is not null)
		{
			return new RunOutcome(RunOutcomeKind.Logged, RunNumber: logged.RunNumber, NewDuty: logged.NewDuty, Pace: logged.Pace);
		}

		if (ConnectionFor(_settings) is null)
		{
			return new RunOutcome(RunOutcomeKind.NotConnected);
		}

		if (!_settings.AutoUpload)
		{
			return new RunOutcome(RunOutcomeKind.AutoUploadOff);
		}

		var kept = _log.Find(run.Id) ?? run;
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
					_ending[id] = run;
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

	public Task ResumeAsync(CancellationToken cancellationToken = default)
	{
		return AutoUploading && _log.NotUploaded.Count > 0 ? _uploader.UploadAsync(cancellationToken) : Task.CompletedTask;
	}
}
