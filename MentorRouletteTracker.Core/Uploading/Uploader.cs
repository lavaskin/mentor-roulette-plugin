using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Storage;

namespace MentorRouletteTracker.Core.Uploading;

public sealed class Uploader
{
	private readonly RunLog _log;
	private readonly IMentorApi _api;
	private readonly Func<ApiConnection?> _connection;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private volatile bool _needsReconnect;
	private volatile string? _problem;

	public Uploader(RunLog log, IMentorApi api, Func<ApiConnection?> connection)
	{
		_log = log;
		_api = api;
		_connection = connection;
	}

	public bool NeedsReconnect => _needsReconnect;

	public string? Problem => _problem;

	public bool IsUploading => _gate.CurrentCount == 0;

	public event Action<IReadOnlyList<(Guid RunId, LoggedRun Logged)>>? Logged;

	public async Task UploadAsync(CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken);
		var logged = new List<(Guid, LoggedRun)>();
		try
		{
			var tried = new HashSet<Guid>();
			while (_log.NotUploaded.FirstOrDefault(run => !tried.Contains(run.Id)) is { } run)
			{
				if (_connection() is not { } connection)
				{
					return;
				}

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
						logged.Add((run.Id, result.Value!));
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
			try
			{
				if (logged.Count > 0)
				{
					Logged?.Invoke(logged);
				}
			}
			finally
			{
				_gate.Release();
			}
		}
	}

	public async Task WaitForPassAsync(CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken);
		_gate.Release();
	}

	public Task RetryAsync(Guid id, CancellationToken cancellationToken = default)
	{
		_log.Update(id, run => run.Status == RunStatus.Failed
			? run with { Status = RunStatus.NotUploaded, FailedReason = null }
			: run);
		return UploadAsync(cancellationToken);
	}

	public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return WhenNoPassAsync(() => _log.Delete(id), cancellationToken);
	}

	public Task<int> DeleteUploadedAsync(CancellationToken cancellationToken = default)
	{
		return WhenNoPassAsync(_log.DeleteUploaded, cancellationToken);
	}

	public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
	{
		return WhenNoPassAsync(_log.DeleteAll, cancellationToken);
	}

	private async Task<T> WhenNoPassAsync<T>(Func<T> change, CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken);
		try
		{
			return change();
		}
		finally
		{
			_gate.Release();
		}
	}

	public void ConnectionChanged()
	{
		_needsReconnect = false;
		_problem = null;
	}
}
