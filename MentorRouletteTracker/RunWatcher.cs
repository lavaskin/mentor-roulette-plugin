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
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Recording;
using MentorRouletteTracker.Core.Storage;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker;

internal sealed unsafe class RunWatcher : IDisposable
{
	private const uint MentorRouletteId = 9;

	private static readonly TimeSpan SaveEvery = TimeSpan.FromMinutes(1);

	private readonly IClientState _clientState;
	private readonly IDutyState _dutyState;
	private readonly IPlayerState _playerState;
	private readonly IFramework _framework;
	private readonly ICondition _condition;
	private readonly IPluginLog _log;
	private readonly GameNames _names;
	private readonly Action<Run> _runEnded;
	private readonly RunRecorder _recorder = new();
	private readonly BackgroundSaver _saver;
	private readonly PartyReader _party;
	private InProgressRun? _snapshot;
	private DateTime _lastSave = DateTime.MinValue;

	private bool _lookedOnStart;

	private bool _partyFailed;

	public RunWatcher(
		IClientState clientState,
		IDutyState dutyState,
		IPlayerState playerState,
		IFramework framework,
		ICondition condition,
		IObjectTable objectTable,
		IPluginLog log,
		GameNames names,
		RunStore store,
		Action<Run> runEnded,
		Func<bool> runsSaved,
		Action<uint> endMissed)
	{
		_clientState = clientState;
		_dutyState = dutyState;
		_playerState = playerState;
		_framework = framework;
		_condition = condition;
		_party = new PartyReader(clientState, condition, objectTable);
		_log = log;
		_names = names;
		_runEnded = runEnded;
		_saver = new BackgroundSaver(
			() =>
			{
				var snapshot = Volatile.Read(ref _snapshot);

				if (snapshot is null && !runsSaved())
				{
					_log.Warning("Kept the run in progress, since the runs couldn't be saved");
					return;
				}

				store.SaveInProgress(snapshot);
			},
			exception => _log.Error(exception, "Couldn't save the run in progress"));

		_recorder.Noted += OnNoted;
		_recorder.EndMissed += endMissed;

		if (store.LoadInProgress() is { } saved)
		{
			_recorder.Restore(saved);
			_snapshot = saved;
		}

		_clientState.CfPop += OnCfPop;
		_clientState.ZoneInit += OnZoneInit;
		_clientState.Logout += OnLogout;
		_dutyState.DutyStarted += OnDutyStarted;
		_dutyState.DutyWiped += OnDutyWiped;
		_dutyState.DutyCompleted += OnDutyCompleted;
		_framework.Update += OnUpdate;
	}

	public void Dispose()
	{
		_clientState.CfPop -= OnCfPop;
		_clientState.ZoneInit -= OnZoneInit;
		_clientState.Logout -= OnLogout;
		_dutyState.DutyStarted -= OnDutyStarted;
		_dutyState.DutyWiped -= OnDutyWiped;
		_dutyState.DutyCompleted -= OnDutyCompleted;
		_framework.Update -= OnUpdate;

		if (_framework.IsFrameworkUnloading)
		{
			Save();
		}
		else
		{
			Volatile.Write(ref _snapshot, _recorder.TurnedOff(DateTime.UtcNow));
			_saver.Request();
		}

		_saver.Flush();
	}

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
			var inProgress = finder->QueueInfo.PoppedContentIsInProgress;
			_log.Debug("Queue pop: {Type} {Id}, in progress {InProgress}", popped.ContentType, popped.Id, inProgress);

			_recorder.Popped(IsMentorRoulette(popped.ContentType, popped.Id), inProgress, DateTime.UtcNow);
		});
	}

	private void OnZoneInit(ZoneInitEventArgs args)
	{
		OnFramework(() => Arrived(args.ContentFinderCondition.RowId, ReadAcceptedPop()));
	}

	private void OnLogout(int type, int code)
	{
		OnFramework(() =>
		{
			_recorder.LoggedOut(DateTime.UtcNow);
			Save();
		});
	}

	private void OnDutyStarted(IDutyStateEventArgs args)
	{
		OnFramework(() =>
		{
			if (_recorder.Commenced(DateTime.UtcNow))
			{
				Save();
			}
		});
	}

	private void OnDutyWiped(IDutyStateEventArgs args)
	{
		OnFramework(() =>
		{
			_recorder.GameWiped(DateTime.UtcNow);
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

		if (_recorder.InDuty && !_clientState.IsLoggedIn)
		{
			_recorder.LoggedOut(now);
			Save();
		}

		if (settled && (!_lookedOnStart || _recorder.IsAway))
		{
			_lookedOnStart = true;
			Arrived(GameMain.Instance()->CurrentContentFinderConditionId);
		}

		if (settled && (_recorder.LoadedIn(now) || (_dutyState.IsDutyStarted && _recorder.Commenced(now, dutyHadStarted: true))))
		{
			Save();
		}

		if (settled && _recorder.InDuty && PartySeen(now))
		{
			Save();
		}

		if (_recorder.NeedsJob && settled && _playerState.ClassJob.RowId != 0)
		{
			_recorder.JobKnown(_playerState.ClassJob.RowId);
			Save();
		}

		if (_recorder.InDuty && now - _lastSave >= SaveEvery)
		{
			Save();
		}
	}

	private void OnFramework(Action action)
	{
		_framework.RunOnFrameworkThread(action).ContinueWith(
			done => _log.Error(done.Exception!, "Couldn't handle a game event"),
			CancellationToken.None,
			TaskContinuationOptions.OnlyOnFaulted,
			TaskScheduler.Default);
	}

	private void Arrived(uint contentFinderConditionId, AcceptedPop? acceptedPop = null)
	{
		var throughDutyFinder = ThroughDutyFinder();
		_log.Debug("Arrived in duty {Duty}, through the duty finder {ThroughDutyFinder}, accepted pop {AcceptedPop}", contentFinderConditionId, throughDutyFinder, acceptedPop?.ToString() ?? "none");
		Finished(_recorder.Arrived(contentFinderConditionId, throughDutyFinder, DateTime.UtcNow, acceptedPop));
	}

	private static AcceptedPop? ReadAcceptedPop()
	{
		var finder = ContentsFinder.Instance();
		if (finder == null || finder->QueueInfo.QueueState != ContentsFinderQueueState.Accepted)
		{
			return null;
		}

		var popped = finder->QueueInfo.PoppedQueueEntry;
		return new AcceptedPop(IsMentorRoulette(popped.ContentType, popped.Id), finder->QueueInfo.PoppedContentIsInProgress);
	}

	private static bool IsMentorRoulette(ContentsType type, uint id)
	{
		return type == ContentsType.Roulette && id == MentorRouletteId;
	}

	private static bool ThroughDutyFinder()
	{
		var finder = ContentsFinder.Instance();
		return finder != null && finder->QueueInfo.QueueState is not (
			ContentsFinderQueueState.None or ContentsFinderQueueState.Pending or ContentsFinderQueueState.Queued);
	}

	private void Finished(Run? run)
	{
		if (run is not null)
		{
			_runEnded(run);
		}

		Save();
	}

	private bool PartySeen(DateTime now)
	{
		try
		{
			return _recorder.PartySeen(_party.Read(), now);
		}
		catch (Exception exception)
		{
			if (!_partyFailed)
			{
				_partyFailed = true;
				_log.Warning(exception, "Couldn't read the party, so wipes only count from the game's wipe message, and deaths don't count, while it fails (logged once a run)");
			}

			return false;
		}
	}

	private void OnNoted(RunNote note)
	{
		if (note is RunNote.Started)
		{
			_partyFailed = false;
		}

		var line = LogLine.Format(note, _names.Duty, _names.Job);
		if (note is RunNote.Ignored)
		{
			_log.Debug("{Line:l}", line);
		}
		else
		{
			_log.Information("{Line:l}", line);
		}
	}

	private void Save()
	{
		var now = DateTime.UtcNow;
		Volatile.Write(ref _snapshot, _recorder.Snapshot(now));
		_lastSave = now;
		_saver.Request();
	}
}
