using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Recording;

public sealed class RunRecorder
{
	public static readonly TimeSpan PopLifetime = TimeSpan.FromMinutes(5);

	private readonly PartyWatcher _party = new();
	private Pop? _pop;
	private Current? _current;

	public RunRecorder()
	{
		_party.Noted += Note;
	}

	public event Action<RunNote>? Noted;

	public event Action<uint>? EndMissed;

	public bool InDuty => _current is { Away: false };

	public bool IsAway => _current is { Away: true };

	public bool NeedsJob => _current is { GameClassJobId: 0, Away: false };

	public void Popped(bool isMentorRoulette, bool inProgress, DateTime at)
	{
		_pop = isMentorRoulette ? new Pop(inProgress, at) : null;
		Note(isMentorRoulette ? new RunNote.Popped(inProgress) : new RunNote.PopIgnored());
	}

	public Run? Arrived(uint contentFinderConditionId, bool throughDutyFinder, DateTime at, AcceptedPop? acceptedPop = null)
	{
		var pop = _pop ?? (acceptedPop is { IsMentorRoulette: true } accepted ? new Pop(accepted.InProgress, at) : null);
		_pop = null;

		Run? finished = null;
		if (_current is { } current)
		{
			if (contentFinderConditionId != 0 && contentFinderConditionId == current.GameContentId)
			{
				if (current.Away)
				{
					if (current.ClockRanWhileOff)
					{
						current.RunningSince = current.LastSeenAt;
						current.ClockRanWhileOff = false;
					}

					current.Away = false;
					_party.Reset();
					Note(new RunNote.BackInRun(current.GameContentId));
				}

				return null;
			}

			finished = Finish(cleared: false, at);
		}

		if (pop is not null && contentFinderConditionId != 0 && throughDutyFinder && at - pop.At <= PopLifetime)
		{
			_current = new Current
			{
				Id = Guid.NewGuid(),
				GameContentId = contentFinderConditionId,
				Replacement = pop.InProgress,
				StartsOn = pop.InProgress ? ClockStart.LoadedIn : ClockStart.Commenced,
				EnteredAt = at,
				LastSeenAt = at,
				Deaths = 0,
			};
			_party.Reset();
			Note(new RunNote.Started(contentFinderConditionId, pop.InProgress));
		}

		return finished;
	}

	public bool LoadedIn(DateTime at)
	{
		if (_current is not { Away: false, RunningSince: null, StartsOn: ClockStart.LoadedIn } current)
		{
			return false;
		}

		current.RunningSince = at;
		Note(new RunNote.ClockStarted(ClockStartReason.LoadedIn));
		return true;
	}

	public bool Commenced(DateTime at, bool dutyHadStarted = false)
	{
		if (_current is not { Away: false, RunningSince: null } current)
		{
			return false;
		}

		current.RunningSince = at;
		Note(new RunNote.ClockStarted(dutyHadStarted ? ClockStartReason.DutyHadStarted : ClockStartReason.DutyCommenced));
		return true;
	}

	public void JobKnown(uint classJobId)
	{
		if (NeedsJob && classJobId != 0)
		{
			_current!.GameClassJobId = classJobId;
			Note(new RunNote.JobKnown(classJobId));
		}
	}

	public bool PartySeen(Party party, DateTime at)
	{
		if (!InDuty)
		{
			return false;
		}

		var frame = _party.Seen(party, at);
		var counted = CountDeaths(frame.Deaths);
		if (frame.Wipe is { } source)
		{
			CountWipe(source, frame.WipeMembers);
			counted = true;
		}

		return counted;
	}

	public void GameWiped(DateTime at)
	{
		if (!InDuty)
		{
			Note(new RunNote.WipeIgnored());
			return;
		}

		if (_party.GameWiped(at) is { } source)
		{
			CountWipe(source, 0);
		}
	}

	private bool CountDeaths(int deaths)
	{
		var current = _current!;
		if (deaths == 0 || current.Deaths is not { } before)
		{
			return false;
		}

		current.Deaths = before + deaths;
		Note(new RunNote.DeathsCounted(before + deaths));
		return true;
	}

	private void CountWipe(WipeSource source, int members)
	{
		var current = _current!;
		current.Wipes++;
		Note(new RunNote.Wiped(current.Wipes, source, members));
	}

	public Run? Completed(DateTime at)
	{
		return InDuty ? Finish(cleared: true, at) : null;
	}

	public void LoggedOut(DateTime at)
	{
		if (_current is not { Away: false } current)
		{
			return;
		}

		StopClock(current, at);
		current.Away = true;
		current.StartsOn = ClockStart.LoadedIn;
		current.LastSeenAt = at;
		Note(new RunNote.LoggedOut((int)current.Elapsed.TotalSeconds));
	}

	public InProgressRun? Snapshot(DateTime at)
	{
		if (_current is not { } current)
		{
			return null;
		}

		var elapsed = current.RunningSince is { } since ? current.Elapsed + (at - since) : current.Elapsed;
		return new InProgressRun
		{
			Id = current.Id,
			GameContentId = current.GameContentId,
			GameClassJobId = current.GameClassJobId,
			Replacement = current.Replacement,
			Wipes = current.Wipes,
			Deaths = current.Deaths,
			ElapsedSeconds = elapsed.TotalSeconds,
			EnteredAt = current.EnteredAt,
			LastSeenAt = current.Away ? current.LastSeenAt : at,
			AwaitingDutyCommenced = current is { StartsOn: ClockStart.Commenced, RunningSince: null },
			PluginTurnedOff = current.PluginWasOff,
			ClockRanWhileOff = current.ClockRanWhileOff,
		};
	}

	public InProgressRun? TurnedOff(DateTime at)
	{
		if (_current is not { } current)
		{
			return null;
		}

		current.PluginWasOff = true;
		current.ClockRanWhileOff |= current is { Away: false, RunningSince: not null };
		return Snapshot(at);
	}

	public void Restore(InProgressRun run)
	{
		_current = new Current
		{
			Id = run.Id,
			GameContentId = run.GameContentId,
			GameClassJobId = run.GameClassJobId,
			Replacement = run.Replacement,
			Wipes = run.Wipes,
			Deaths = run.Deaths,
			Elapsed = TimeSpan.FromSeconds(run.ElapsedSeconds),
			Away = true,
			StartsOn = run.AwaitingDutyCommenced ? ClockStart.Commenced : ClockStart.LoadedIn,
			EnteredAt = run.EnteredAt,
			LastSeenAt = run.LastSeenAt,
			PluginWasOff = run.PluginTurnedOff,
			ClockRanWhileOff = run.ClockRanWhileOff,
		};
		Note(new RunNote.Restored(run.GameContentId, run.PluginTurnedOff));
	}

	private Run? Finish(bool cleared, DateTime at)
	{
		var current = _current!;
		_current = null;

		if (current.PluginWasOff && !cleared)
		{
			Note(new RunNote.Dropped(current.GameContentId, DropReason.PluginWasOff));
			EndMissed?.Invoke(current.GameContentId);
			return null;
		}

		if (current.GameClassJobId == 0)
		{
			Note(new RunNote.Dropped(current.GameContentId, DropReason.NeverLoadedIn));
			return null;
		}

		var endedAt = current.Away ? current.LastSeenAt : at;
		StopClock(current, endedAt);
		var enteredAt = current.EnteredAt ?? endedAt - current.Elapsed;

		var deaths = current.Deaths;
		if (deaths > Run.MaxDeaths)
		{
			Note(new RunNote.TooManyDeaths(deaths.Value));
			deaths = null;
		}

		var run = new Run
		{
			Id = current.Id,
			GameContentId = current.GameContentId,
			GameClassJobId = current.GameClassJobId,
			Cleared = cleared,
			Replacement = current.Replacement,
			Wipes = current.Wipes,
			Deaths = deaths,
			DurationSeconds = Math.Max(1, (int)current.Elapsed.TotalSeconds),
			DatePlayed = DateTime.SpecifyKind(enteredAt, DateTimeKind.Utc),
		};
		Note(new RunNote.Ended(run, current.Away));
		return run;
	}

	private void Note(RunNote note)
	{
		Noted?.Invoke(note);
	}

	private static void StopClock(Current current, DateTime at)
	{
		if (current.RunningSince is { } since)
		{
			current.Elapsed += at - since;
			current.RunningSince = null;
		}
	}

	private sealed record Pop(bool InProgress, DateTime At);

	private enum ClockStart
	{
		Commenced,

		LoadedIn,
	}

	private sealed class Current
	{
		public required Guid Id { get; init; }
		public required uint GameContentId { get; init; }
		public uint GameClassJobId { get; set; }
		public required bool Replacement { get; init; }
		public int Wipes { get; set; }

		public int? Deaths { get; set; }

		public TimeSpan Elapsed { get; set; }

		public DateTime? RunningSince { get; set; }

		public required ClockStart StartsOn { get; set; }

		public required DateTime? EnteredAt { get; init; }

		public bool Away { get; set; }

		public required DateTime LastSeenAt { get; set; }

		public bool PluginWasOff { get; set; }

		public bool ClockRanWhileOff { get; set; }
	}
}
