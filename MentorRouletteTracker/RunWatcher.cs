using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.DutyState;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ContentFinderCondition = Lumina.Excel.Sheets.ContentFinderCondition;
using MentorRouletteTracker.Core;

namespace MentorRouletteTracker;

/// <summary>
/// Turns the game's events into <see cref="RunRecorder"/>'s, on the framework thread, and saves the run in progress
/// on every change and every minute, so a crash loses at most a minute of its time.
/// </summary>
internal sealed unsafe class RunWatcher : IDisposable
{
	/// <summary>
	/// <c>ContentRoulette</c> row 9, "Duty Roulette: Mentor". The only thing the plugin ever records.
	/// </summary>
	private const uint MentorRouletteId = 9;

	private static readonly TimeSpan SaveEvery = TimeSpan.FromMinutes(1);

	private readonly IClientState _clientState;
	private readonly IDutyState _dutyState;
	private readonly IPlayerState _playerState;
	private readonly IFramework _framework;
	private readonly ICondition _condition;
	private readonly IPluginLog _log;
	private readonly Action<Run> _runEnded;
	private readonly RunRecorder _recorder = new();
	private readonly BackgroundSaver _saver;
	private InProgressRun? _snapshot;
	private DateTime _lastSave = DateTime.MinValue;

	/// <summary>
	/// Whether the plugin has looked at where the player is since it started
	/// </summary>
	private bool _lookedOnStart;

	/// <param name="runEnded">Each finished run, on the framework thread</param>
	public RunWatcher(
		IClientState clientState,
		IDutyState dutyState,
		IPlayerState playerState,
		IFramework framework,
		ICondition condition,
		IPluginLog log,
		RunStore store,
		Action<Run> runEnded)
	{
		_clientState = clientState;
		_dutyState = dutyState;
		_playerState = playerState;
		_framework = framework;
		_condition = condition;
		_log = log;
		_runEnded = runEnded;
		_saver = new BackgroundSaver(
			() => store.SaveInProgress(Volatile.Read(ref _snapshot)),
			exception => _log.Error(exception, "Couldn't save the run in progress"));

		if (store.LoadInProgress() is { } saved)
		{
			_log.Information("Found a run in progress from before the plugin started, in duty {Duty}", saved.GameContentId);
			_recorder.Restore(saved);
			_snapshot = saved;
		}

		_clientState.CfPop += OnCfPop;
		_clientState.ZoneInit += OnZoneInit;
		_clientState.Logout += OnLogout;
		_dutyState.DutyWiped += OnDutyWiped;
		_dutyState.DutyCompleted += OnDutyCompleted;
		_framework.Update += OnUpdate;
	}

	public void Dispose()
	{
		_clientState.CfPop -= OnCfPop;
		_clientState.ZoneInit -= OnZoneInit;
		_clientState.Logout -= OnLogout;
		_dutyState.DutyWiped -= OnDutyWiped;
		_dutyState.DutyCompleted -= OnDutyCompleted;
		_framework.Update -= OnUpdate;

		// The time up to now counts, and the time until the plugin's back doesn't
		Save();
		_saver.Flush();
	}

	// Dalamud raises CfPop off the framework thread
	private void OnCfPop(ContentFinderCondition _)
	{
		OnFramework(() =>
		{
			var finder = ContentsFinder.Instance();
			if (finder == null)
			{
				return;
			}

			var popped = finder->QueueInfo.PoppedQueueEntry;
			var isMentorRoulette = popped.ContentType == ContentsType.Roulette && popped.Id == MentorRouletteId;
			var inProgress = finder->QueueInfo.PoppedContentIsInProgress;
			_log.Debug("Queue pop: {Type} {Id}, in progress {InProgress}", popped.ContentType, popped.Id, inProgress);

			_recorder.Popped(isMentorRoulette, inProgress, DateTime.UtcNow);
		});
	}

	private void OnZoneInit(ZoneInitEventArgs args)
	{
		OnFramework(() => Arrived(args.ContentFinderCondition.RowId));
	}

	private void OnLogout(int type, int code)
	{
		OnFramework(() =>
		{
			_recorder.LoggedOut(DateTime.UtcNow);
			Save();
		});
	}

	private void OnDutyWiped(IDutyStateEventArgs args)
	{
		OnFramework(() =>
		{
			_recorder.Wiped();
			Save();
		});
	}

	private void OnDutyCompleted(IDutyStateEventArgs args)
	{
		OnFramework(() => Finished(_recorder.Completed(DateTime.UtcNow)));
	}

	private void OnUpdate(IFramework framework)
	{
		var now = DateTime.UtcNow;
		var settled = _clientState.IsLoggedIn
			&& _playerState.IsLoaded
			&& !_condition[ConditionFlag.BetweenAreas]
			&& !_condition[ConditionFlag.BetweenAreas51];

		// Logout isn't raised on every way out of the game, and the clock mustn't run while they're gone
		if (_recorder.IsRunning && !_clientState.IsLoggedIn)
		{
			_recorder.LoggedOut(now);
			Save();
		}

		// A paused run (logged out, or saved before the plugin started) carries on or ends once the player's in the
		// world again. Zone changes say where they arrive too, but on logging in that can come before the game
		// counts them as logged in.
		if (settled && (!_lookedOnStart || _recorder.IsPaused))
		{
			_lookedOnStart = true;
			Arrived(GameMain.Instance()->CurrentContentFinderConditionId);
		}

		if (_recorder.NeedsJob && settled && _playerState.ClassJob.RowId != 0)
		{
			_recorder.JobKnown(_playerState.ClassJob.RowId);
			Save();
		}

		if (_recorder.IsRunning && now - _lastSave >= SaveEvery)
		{
			Save();
		}
	}

	/// <summary>
	/// Runs <paramref name="action"/> on the framework thread (straight away if already on it), since the recorder
	/// isn't thread safe and the game's memory is read there
	/// </summary>
	private void OnFramework(Action action)
	{
		_framework.RunOnFrameworkThread(action).ContinueWith(
			done => _log.Error(done.Exception!, "Couldn't handle a game event"),
			CancellationToken.None,
			TaskContinuationOptions.OnlyOnFaulted,
			TaskScheduler.Default);
	}

	private void Arrived(uint contentFinderConditionId)
	{
		var throughDutyFinder = ThroughDutyFinder();
		_log.Debug("Arrived in duty {Duty}, through the duty finder {ThroughDutyFinder}", contentFinderConditionId, throughDutyFinder);
		Finished(_recorder.Arrived(contentFinderConditionId, throughDutyFinder, DateTime.UtcNow));
	}

	/// <summary>
	/// The duty finder's queue is past the pop: the player's going into, or is in, what it popped. After a pop is
	/// declined or withdrawn it's back to queued or none.
	/// </summary>
	private static bool ThroughDutyFinder()
	{
		var finder = ContentsFinder.Instance();
		return finder != null && finder->QueueInfo.QueueState is not (
			ContentsFinderQueueState.None or ContentsFinderQueueState.Pending or ContentsFinderQueueState.Queued);
	}

	private void Finished(Run? run)
	{
		// Kept before the run in progress is cleared, so a crash in between can't lose it
		if (run is not null)
		{
			_log.Information("Run ended: duty {Duty}, cleared {Cleared}, {Wipes} wipes, {Seconds}s", run.GameContentId, run.Cleared, run.Wipes, run.DurationSeconds);
			_runEnded(run);
		}

		Save();
	}

	private void Save()
	{
		var now = DateTime.UtcNow;
		Volatile.Write(ref _snapshot, _recorder.Snapshot(now));
		_lastSave = now;
		_saver.Request();
	}
}
