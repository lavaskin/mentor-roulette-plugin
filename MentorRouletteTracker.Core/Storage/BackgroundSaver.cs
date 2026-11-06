namespace MentorRouletteTracker.Core.Storage;

/// <summary>
/// Runs a save off the calling thread, so file writes never hold up the game's frame. Saves run one at a time, and
/// requests made while one is running are folded into one more, so the last request is always followed by a save
/// that sees everything up to it. The save reads what to write when it runs.
/// </summary>
public sealed class BackgroundSaver
{
	private readonly Action _save;
	private readonly Action<Exception> _onError;
	private readonly Lock _lock = new();
	private bool _requested;
	private Task _worker = Task.CompletedTask;
	private bool _running;
	private bool _lastFailed;

	public BackgroundSaver(Action save, Action<Exception> onError)
	{
		_save = save;
		_onError = onError;
	}

	public void Request()
	{
		lock (_lock)
		{
			_requested = true;
			if (!_running)
			{
				_running = true;
				_worker = Task.Run(Drain);
			}
		}
	}

	/// <summary>
	/// Waits for every requested save to finish
	/// </summary>
	/// <returns>Whether the last save worked</returns>
	public bool Flush()
	{
		while (true)
		{
			Task worker;
			lock (_lock)
			{
				if (!_running)
				{
					return !_lastFailed;
				}

				worker = _worker;
			}

			worker.Wait();
		}
	}

	private void Drain()
	{
		while (true)
		{
			lock (_lock)
			{
				if (!_requested)
				{
					_running = false;
					return;
				}

				_requested = false;
			}

			var failed = false;
			try
			{
				_save();
			}
			catch (Exception exception)
			{
				failed = true;
				_onError(exception);
			}

			lock (_lock)
			{
				_lastFailed = failed;
			}
		}
	}
}
