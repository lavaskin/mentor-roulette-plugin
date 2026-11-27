using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.DutyState;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ContentFinderCondition = Lumina.Excel.Sheets.ContentFinderCondition;
using GameGroupManager = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager;
using GamePartyMember = FFXIVClientStructs.FFXIV.Client.Game.Group.PartyMember;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Recording;
using MentorRouletteTracker.Core.Storage;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker;

/// <summary>
/// Turns the game's events into <see cref="RunRecorder"/>'s, on the framework thread, and saves the run in progress
/// on every change and every minute, so a crash loses at most a minute of its time. While the player's in a run's duty
/// and loaded in, it reads the party every frame for the wipes. What the recorder notes goes in Dalamud's log, at
/// Information during a run, so it's there at the default level when a run's recorded wrong.
/// </summary>
internal sealed unsafe class RunWatcher : IDisposable
{
	/// <summary>
	/// <c>ContentRoulette</c> row 9, "Duty Roulette: Mentor". The only thing the plugin ever records.
	/// </summary>
	private const uint MentorRouletteId = 9;

	/// <summary>
	/// <see cref="GamePartyMember.Flags"/>: set for an alliance slot that's in use
	/// </summary>
	private const byte AllianceSlotInUse = 0x01;

	private static readonly TimeSpan SaveEvery = TimeSpan.FromMinutes(1);

	private readonly IClientState _clientState;
	private readonly IDutyState _dutyState;
	private readonly IPlayerState _playerState;
	private readonly IFramework _framework;
	private readonly ICondition _condition;
	private readonly IObjectTable _objectTable;
	private readonly IPluginLog _log;
	private readonly GameNames _names;
	private readonly Action<Run> _runEnded;
	private readonly RunRecorder _recorder = new();
	private readonly BackgroundSaver _saver;
	private readonly List<PartySlot> _slots = [];
	private InProgressRun? _snapshot;
	private DateTime _lastSave = DateTime.MinValue;

	/// <summary>
	/// Whether the plugin has looked at where the player is since it started
	/// </summary>
	private bool _lookedOnStart;

	/// <param name="runEnded">Each finished run, on the framework thread, which keeps it and asks for it to be saved</param>
	/// <param name="runsSaved">Waits for the kept runs to be saved, and says whether they were</param>
	/// <param name="endMissed">
	/// The duty of a run dropped because the plugin was off for part of it and didn't see it cleared, on the framework
	/// thread
	/// </param>
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
		_objectTable = objectTable;
		_log = log;
		_names = names;
		_runEnded = runEnded;
		_saver = new BackgroundSaver(
			() =>
			{
				var snapshot = Volatile.Read(ref _snapshot);

				// The run that ended is cleared only once it's in the runs file, so a crash can't lose it. If that save
				// failed, the old file stays, and carries the run on or ends it again next time.
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

		// Dalamud unloads when the game closes (or when it's unloaded itself), which is treated like a crash: the time up
		// to now counts, and the time until the plugin's back doesn't. Otherwise only the plugin's going (turned off,
		// reloaded or updated), and it may miss how the run ends.
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

	// "Duty commenced", which starts the clock like the game's completion time. It doesn't fire for a replacement.
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

		// Logout isn't raised on every way out of the game, and the clock mustn't run while they're gone
		if (_recorder.InDuty && !_clientState.IsLoggedIn)
		{
			_recorder.LoggedOut(now);
			Save();
		}

		// A run the player's away from (logged out, or saved before the plugin started) carries on or ends once
		// they're in the world again. Zone changes say where they arrive too, but on logging in that can come before
		// the game counts them as logged in.
		if (settled && (!_lookedOnStart || _recorder.IsAway))
		{
			_lookedOnStart = true;
			Arrived(GameMain.Instance()->CurrentContentFinderConditionId);
		}

		// A replacement's clock, or one back from a disconnect, starts once they've loaded in. Dalamud also counts the
		// duty as started on the first combat in it, in case "Duty commenced" was missed.
		if (settled && (_recorder.LoadedIn(now) || (_dutyState.IsDutyStarted && _recorder.Commenced(now, dutyHadStarted: true))))
		{
			Save();
		}

		if (settled && _recorder.InDuty && _recorder.PartySeen(ReadParty(), now))
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

	private void Arrived(uint contentFinderConditionId, AcceptedPop? acceptedPop = null)
	{
		var throughDutyFinder = ThroughDutyFinder();
		_log.Debug("Arrived in duty {Duty}, through the duty finder {ThroughDutyFinder}, accepted pop {AcceptedPop}", contentFinderConditionId, throughDutyFinder, acceptedPop?.ToString() ?? "none");
		Finished(_recorder.Arrived(contentFinderConditionId, throughDutyFinder, DateTime.UtcNow, acceptedPop));
	}

	/// <summary>
	/// The pop the duty finder holds while it's accepted: the player's on their way into what it popped. Not once
	/// they're in it (in content), so a zone change inside a duty, or after clearing it, can't start a run.
	/// </summary>
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
		// Kept before the run in progress is cleared, whose save waits for the runs' (above), so a crash can't lose it
		if (run is not null)
		{
			_runEnded(run);
		}

		Save();
	}

	private void OnNoted(RunNote note)
	{
		var line = LogLine.Format(note, _names.Duty, _names.Job);
		if (note.Detail)
		{
			_log.Debug("{Line:l}", line);
		}
		else
		{
			_log.Information("{Line:l}", line);
		}
	}

	/// <summary>
	/// The party as this frame shows it: the party's slots, the alliance's in an alliance raid, and the player. Whether
	/// others are in combat can only be read while they're showing in the game.
	/// </summary>
	private Party ReadParty()
	{
		_slots.Clear();
		var player = _objectTable.LocalPlayer;
		var playerId = player?.EntityId ?? 0;
		var group = GameGroupManager.Instance();
		if (group != null)
		{
			ref var main = ref group->MainGroup;
			var count = Math.Min((int)main.MemberCount, main.PartyMembers.Length);
			for (var i = 0; i < count; i++)
			{
				_slots.Add(Slot(in main.PartyMembers[i], otherParty: false, playerId));
			}

			if (main.IsAlliance)
			{
				foreach (ref readonly var member in main.AllianceMembers)
				{
					if ((member.Flags & AllianceSlotInUse) != 0)
					{
						_slots.Add(Slot(in member, otherParty: true, playerId));
					}
				}
			}
		}

		var self = new PartySlot(
			playerId,
			player?.CurrentHp ?? 0,
			player?.MaxHp ?? 0,
			_clientState.TerritoryType,
			_condition[ConditionFlag.InCombat]);
		return PartyReading.Read(_slots, self);
	}

	private PartySlot Slot(in GamePartyMember member, bool otherParty, uint playerId)
	{
		var inCombat = member.CurrentHP > 0 && (member.EntityId == playerId
			? _condition[ConditionFlag.InCombat]
			: _objectTable.SearchByEntityId(member.EntityId) is ICharacter character && character.StatusFlags.HasFlag(StatusFlags.InCombat));
		return new PartySlot(member.EntityId, member.CurrentHP, member.MaxHP, member.TerritoryType, inCombat, otherParty);
	}

	private void Save()
	{
		var now = DateTime.UtcNow;
		Volatile.Write(ref _snapshot, _recorder.Snapshot(now));
		_lastSave = now;
		_saver.Request();
	}
}
