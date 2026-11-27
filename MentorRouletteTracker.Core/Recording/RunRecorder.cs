using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Recording;

/// <summary>
/// Turns what happens in the game, as plain events with their times, into finished Mentor Roulette runs. Each event
/// returns the run it finished, if any.
/// <para>
/// A run starts when the player arrives in a duty from a Mentor Roulette pop, and ends at the duty-complete message
/// (cleared) or when they arrive anywhere else (not cleared).
/// </para>
/// <para>
/// Its date played is when the player entered the duty: the arrival that started it, at the start of the loading
/// screen in. Not when it ended, so a run that goes past midnight counts for the day it started. A disconnect and
/// return keeps the first entry.
/// </para>
/// <para>
/// Its time is the player's own, and matches the game's completion time for a normal run: the clock starts at "Duty
/// commenced". A replacement joins after that, so their clock starts once they've loaded in. The clock stops while
/// they're logged out, and starts again once they've loaded back into the duty. Loading screens never count.
/// </para>
/// <para>
/// A run the player never loaded into (they disconnected or crashed on the loading screen, and didn't come back)
/// gives out nothing: it has no job, which the site needs, and no time.
/// </para>
/// <para>
/// A run the plugin was turned off for part of (<see cref="TurnedOff"/>) may have been cleared while it was off, so
/// it's given out only if the recorder sees the clear. Ending any other way drops it, and raises
/// <see cref="EndMissed"/>.
/// </para>
/// <para>
/// Its wipes are the party's, from <see cref="PartyWatcher"/>: everyone in the party dying in the same fight, or the
/// game's wipe message. Only while the player's in the duty: the party's seen afresh each time they're in it, at the
/// run's start and on coming back.
/// </para>
/// Not thread safe: the plugin calls it from the game's framework thread only.
/// </summary>
public sealed class RunRecorder
{
	/// <summary>
	/// How long after a pop the player can arrive in its duty. A pop is answered within 45 seconds, then the party
	/// loads in, so an older one was withdrawn, declined or left to expire.
	/// </summary>
	public static readonly TimeSpan PopLifetime = TimeSpan.FromMinutes(5);

	private readonly PartyWatcher _party = new();
	private Pop? _pop;
	private Current? _current;

	public RunRecorder()
	{
		_party.Noted += Note;
	}

	/// <summary>
	/// What it saw and decided, for the log
	/// </summary>
	public event Action<RunNote>? Noted;

	/// <summary>
	/// A run the plugin was turned off for part of ended without the recorder seeing the clear, and was dropped. Gives
	/// its duty (<c>ContentFinderCondition</c>).
	/// </summary>
	public event Action<uint>? EndMissed;

	/// <summary>
	/// A run is in progress, with the player in its duty (its clock may not have started yet)
	/// </summary>
	public bool InDuty => _current is { Away: false };

	/// <summary>
	/// A run is in progress while the player's away from it: they logged out, or the plugin restored it on starting.
	/// The next arrival carries it on or ends it.
	/// </summary>
	public bool IsAway => _current is { Away: true };

	/// <summary>
	/// The run in progress, with the player in its duty, doesn't have the player's job yet
	/// </summary>
	public bool NeedsJob => _current is { GameClassJobId: 0, Away: false };

	/// <summary>
	/// The duty finder popped. Only a Mentor Roulette pop can start a run, and any other replaces it.
	/// </summary>
	/// <param name="isMentorRoulette">The pop is the Mentor Roulette (a roulette, <c>ContentRoulette</c> 9)</param>
	/// <param name="inProgress">It's a duty in progress, so the player is a replacement</param>
	public void Popped(bool isMentorRoulette, bool inProgress, DateTime at)
	{
		_pop = isMentorRoulette ? new Pop(inProgress, at) : null;
		Note(isMentorRoulette ? new RunNote.Popped(inProgress) : new RunNote.PopIgnored());
	}

	/// <summary>
	/// The player arrived in a zone: after a zone change, logging in, or the plugin starting. They may still be
	/// loading in.
	/// </summary>
	/// <param name="contentFinderConditionId">The zone's duty (<c>ContentFinderCondition</c>), or 0 if it isn't one</param>
	/// <param name="throughDutyFinder">
	/// The duty finder put them there: its queue is past the pop. False rules out a duty entered any other way just
	/// after a pop that was declined.
	/// </param>
	/// <param name="acceptedPop">
	/// The pop the duty finder holds as accepted, read on arriving in a zone: the player's on their way in from it.
	/// Used when there's no pop from <see cref="Popped"/>, which the plugin forgets if it's reloaded (or installed)
	/// between the pop and arriving.
	/// </param>
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
			};
			_party.Reset();
			Note(new RunNote.Started(contentFinderConditionId, pop.InProgress));
		}

		return finished;
	}

	/// <summary>
	/// The player has loaded in: logged in, in the world, and not between areas. Starts a replacement's clock, or
	/// one coming back to the duty.
	/// </summary>
	/// <returns>Whether it started the clock</returns>
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

	/// <summary>
	/// "Duty commenced": starts the clock, if it hasn't started
	/// </summary>
	/// <param name="dutyHadStarted">
	/// It's the game saying the duty has started, not the message, which can be missed (the plugin reloaded as it came)
	/// </param>
	/// <returns>Whether it started the clock</returns>
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

	/// <summary>
	/// The player's job, read once they're in the duty. A job can't change inside one.
	/// </summary>
	public void JobKnown(uint classJobId)
	{
		if (NeedsJob && classJobId != 0)
		{
			_current!.GameClassJobId = classJobId;
			Note(new RunNote.JobKnown(classJobId));
		}
	}

	/// <summary>
	/// What one frame shows of the party, once the player's loaded in. Counts a wipe if it finished one.
	/// </summary>
	/// <returns>Whether it counted a wipe</returns>
	public bool PartySeen(Party party, DateTime at)
	{
		if (!InDuty || _party.Seen(party, at) is not var (source, members))
		{
			return false;
		}

		CountWipe(source, members);
		return true;
	}

	/// <summary>
	/// The game's wipe message, when it resets the whole party. Counts a wipe unless it was already counted from the
	/// party.
	/// </summary>
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

	private void CountWipe(WipeSource source, int members)
	{
		var current = _current!;
		current.Wipes++;
		Note(new RunNote.Wiped(current.Wipes, source, members));
	}

	/// <summary>
	/// The duty-complete message: the run is cleared, and its time ends here
	/// </summary>
	public Run? Completed(DateTime at)
	{
		return InDuty ? Finish(cleared: true, at) : null;
	}

	/// <summary>
	/// The player logged out or disconnected. The clock stops until they've loaded back into the duty.
	/// </summary>
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

	/// <summary>
	/// The run in progress, to save, or null if there isn't one. Its time is counted up to <paramref name="at"/>.
	/// </summary>
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
			ElapsedSeconds = elapsed.TotalSeconds,
			EnteredAt = current.EnteredAt,
			LastSeenAt = current.Away ? current.LastSeenAt : at,
			AwaitingDutyCommenced = current is { StartsOn: ClockStart.Commenced, RunningSince: null },
			PluginTurnedOff = current.PluginWasOff,
			ClockRanWhileOff = current.ClockRanWhileOff,
		};
	}

	/// <summary>
	/// The plugin is being turned off, reloaded or updated, rather than the game closing: the run in progress to save,
	/// marked so it's logged only if the clear is seen after the plugin's back. Null if there isn't one.
	/// </summary>
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

	/// <summary>
	/// Carries on with a saved run after the plugin starts. It's treated as logged out since it was last seen, so
	/// the time in between doesn't count: arriving back in its duty carries on once they've loaded in, and arriving
	/// anywhere else ends it. A run saved before "Duty commenced" still waits for it (the plugin was reloaded
	/// while the party loaded in), so the wait doesn't count. The exception is a run the plugin was turned off for
	/// with its clock running: back in its duty, the time it was off counts.
	/// </summary>
	public void Restore(InProgressRun run)
	{
		_current = new Current
		{
			Id = run.Id,
			GameContentId = run.GameContentId,
			GameClassJobId = run.GameClassJobId,
			Replacement = run.Replacement,
			Wipes = run.Wipes,
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

		var run = new Run
		{
			Id = current.Id,
			GameContentId = current.GameContentId,
			GameClassJobId = current.GameClassJobId,
			Cleared = cleared,
			Replacement = current.Replacement,
			Wipes = current.Wipes,
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

	/// <summary>
	/// What starts a run's clock
	/// </summary>
	private enum ClockStart
	{
		/// <summary>
		/// "Duty commenced", like the game's own completion time
		/// </summary>
		Commenced,

		/// <summary>
		/// Loading in: for a replacement, who joins after the duty commenced, or coming back to the duty
		/// </summary>
		LoadedIn,
	}

	private sealed class Current
	{
		public required Guid Id { get; init; }
		public required uint GameContentId { get; init; }
		public uint GameClassJobId { get; set; }
		public required bool Replacement { get; init; }
		public int Wipes { get; set; }

		/// <summary>
		/// Time in the duty before <see cref="RunningSince"/>
		/// </summary>
		public TimeSpan Elapsed { get; set; }

		/// <summary>
		/// When the clock last started, or null while it's stopped
		/// </summary>
		public DateTime? RunningSince { get; set; }

		public required ClockStart StartsOn { get; set; }

		/// <summary>
		/// When the player entered the duty: the run's date played. Null for a run restored from a version that didn't
		/// save it, which is dated at its end minus its time.
		/// </summary>
		public required DateTime? EnteredAt { get; init; }

		/// <summary>
		/// Logged out, or restored and not yet back: the next arrival decides
		/// </summary>
		public bool Away { get; set; }

		public required DateTime LastSeenAt { get; set; }

		/// <summary>
		/// The plugin was turned off for part of the run, so it may have missed the clear
		/// </summary>
		public bool PluginWasOff { get; set; }

		/// <summary>
		/// Turned off with the clock running, and not back in the duty yet: back in it, the clock carries on from
		/// <see cref="LastSeenAt"/>
		/// </summary>
		public bool ClockRanWhileOff { get; set; }
	}
}
