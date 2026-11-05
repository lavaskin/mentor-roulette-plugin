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
/// Its time is the player's own, and matches the game's completion time for a normal run: the clock starts at "Duty
/// commenced". A replacement joins after that, so their clock starts once they've loaded in. The clock stops while
/// they're logged out, and starts again once they've loaded back into the duty. Loading screens never count.
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

	private Pop? _pop;
	private Current? _current;

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
				current.Away = false;
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
				LastSeenAt = at,
			};
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
		return true;
	}

	/// <summary>
	/// "Duty commenced": starts the clock, if it hasn't started
	/// </summary>
	/// <returns>Whether it started the clock</returns>
	public bool Commenced(DateTime at)
	{
		if (_current is not { Away: false, RunningSince: null } current)
		{
			return false;
		}

		current.RunningSince = at;
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
		}
	}

	public void Wiped()
	{
		if (InDuty)
		{
			_current!.Wipes++;
		}
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
			LastSeenAt = current.Away ? current.LastSeenAt : at,
		};
	}

	/// <summary>
	/// Carries on with a saved run after the plugin starts. It's treated as logged out since it was last seen, so
	/// the time in between doesn't count: arriving back in its duty carries on once they've loaded in, and arriving
	/// anywhere else ends it.
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
			StartsOn = ClockStart.LoadedIn,
			LastSeenAt = run.LastSeenAt,
		};
	}

	private Run Finish(bool cleared, DateTime at)
	{
		var current = _current!;
		_current = null;

		var endedAt = current.Away ? current.LastSeenAt : at;
		StopClock(current, endedAt);

		return new Run
		{
			Id = current.Id,
			GameContentId = current.GameContentId,
			GameClassJobId = current.GameClassJobId,
			Cleared = cleared,
			Replacement = current.Replacement,
			Wipes = current.Wipes,
			DurationSeconds = Math.Max(1, (int)current.Elapsed.TotalSeconds),
			DatePlayed = DateTime.SpecifyKind(endedAt, DateTimeKind.Utc),
		};
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
		/// Logged out, or restored and not yet back: the next arrival decides
		/// </summary>
		public bool Away { get; set; }

		public required DateTime LastSeenAt { get; set; }
	}
}
