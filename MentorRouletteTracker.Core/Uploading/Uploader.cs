using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Storage;

namespace MentorRouletteTracker.Core.Uploading;

/// <summary>
/// Uploads the runs that aren't uploaded, oldest first, always: the site numbers runs in the order they arrive, and
/// can't reorder them.
/// <list type="bullet">
/// <item>The site can't be reached, or can't answer: the run stays waiting and holds up the runs after it.</item>
/// <item>A 401: uploading stops, and <see cref="NeedsReconnect"/> says to reconnect. Nothing is marked failed.</item>
/// <item>A 400: the run is failed with the site's message, and set aside so the runs after it carry on.</item>
/// </list>
/// A retry is safe: the run's plugin run id means the site returns it instead of logging it twice.
/// </summary>
public sealed class Uploader
{
	private readonly RunLog _log;
	private readonly IMentorApi _api;
	private readonly Func<ApiConnection?> _connection;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private volatile bool _needsReconnect;
	private volatile string? _problem;

	/// <param name="connection">Where to upload, or null when not connected</param>
	public Uploader(RunLog log, IMentorApi api, Func<ApiConnection?> connection)
	{
		_log = log;
		_api = api;
		_connection = connection;
	}

	/// <summary>
	/// The site stopped accepting the token (a 401), so auto-upload is paused until the user connects again
	/// </summary>
	public bool NeedsReconnect => _needsReconnect;

	/// <summary>
	/// Why the runs waiting to upload didn't go on the last try, or null if they did
	/// </summary>
	public string? Problem => _problem;

	public bool IsUploading => _gate.CurrentCount == 0;

	/// <summary>
	/// Uploads every run that isn't uploaded, oldest first, until one has to wait. Runs that end while it's
	/// uploading go in the same pass. If a pass is already running, waits for it, then makes its own.
	/// </summary>
	public async Task UploadAsync(CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			if (_connection() is not { } connection)
			{
				return;
			}

			var tried = new HashSet<Guid>();
			while (_log.NotUploaded.FirstOrDefault(run => !tried.Contains(run.Id)) is { } run)
			{
				tried.Add(run.Id);
				var result = await _api.LogRunAsync(connection, run, cancellationToken);
				switch (result.Kind)
				{
					case ApiResultKind.Ok:
						_log.Update(run.Id, current => current with
						{
							Status = RunStatus.Uploaded,
							SiteRunId = result.Value!.Id,
							RunNumber = result.Value.RunNumber,
							FailedReason = null,
						});
						_needsReconnect = false;
						_problem = null;
						break;

					case ApiResultKind.Refused:
						_log.Update(run.Id, current => current with
						{
							Status = RunStatus.Failed,
							FailedReason = result.Message,
						});
						_needsReconnect = false;
						break;

					case ApiResultKind.Unauthorized:
						_needsReconnect = true;
						return;

					default:
						_problem = result.Message;
						return;
				}
			}

			_problem = null;
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <summary>
	/// Puts a failed run back in line, then uploads. It goes in its place by date, after any older runs waiting.
	/// </summary>
	public Task RetryAsync(Guid id, CancellationToken cancellationToken = default)
	{
		_log.Update(id, run => run.Status == RunStatus.Failed
			? run with { Status = RunStatus.NotUploaded, FailedReason = null }
			: run);
		return UploadAsync(cancellationToken);
	}

	/// <summary>
	/// The user connected with a token the site accepted, or disconnected, so what went wrong before no longer applies
	/// </summary>
	public void ConnectionChanged()
	{
		_needsReconnect = false;
		_problem = null;
	}
}
