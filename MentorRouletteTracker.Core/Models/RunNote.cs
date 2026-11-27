namespace MentorRouletteTracker.Core.Models;

/// <summary>
/// Something the recorder saw or decided, for the log. None of them say who anyone is: party members are counts only.
/// </summary>
public abstract record RunNote
{
	/// <summary>
	/// A game event with no run to apply to, which the log keeps only at its most detailed level
	/// </summary>
	public abstract record Ignored : RunNote;

	public sealed record Popped(bool Replacement) : RunNote;

	/// <summary>
	/// A pop for anything but the Mentor Roulette
	/// </summary>
	public sealed record PopIgnored : Ignored;

	/// <summary>
	/// A run saved before the plugin started, carried on until the player's seen
	/// </summary>
	public sealed record Restored(uint Duty, bool PluginWasOff) : RunNote;

	public sealed record Started(uint Duty, bool Replacement) : RunNote;

	/// <summary>
	/// Back in the run's duty, after logging out or the plugin starting
	/// </summary>
	public sealed record BackInRun(uint Duty) : RunNote;

	public sealed record ClockStarted(ClockStartReason Reason) : RunNote;

	public sealed record JobKnown(uint Job) : RunNote;

	public sealed record LoggedOut(int ClockSeconds) : RunNote;

	/// <param name="Members">The player's own party</param>
	/// <param name="Alliance">Everyone in the alliance, or 0 outside one</param>
	/// <param name="NotHere">Members left out, since they haven't loaded or aren't in the zone</param>
	public sealed record PartyChanged(int Members, int Alliance, int NotHere) : RunNote;

	public sealed record FightStarted : RunNote;

	public sealed record FightOver(FightEnd Reason) : RunNote;

	/// <summary>
	/// A member of the party died. The counts are the player's own party's, or the whole alliance's for a member of
	/// another party.
	/// </summary>
	/// <param name="DiedThisFight">Null outside a fight</param>
	/// <param name="OtherParty">In another party of the alliance</param>
	public sealed record MemberDied(int Dead, int? DiedThisFight, int Of, bool OtherParty = false) : RunNote;

	/// <summary>
	/// A member of the party is alive again: raised, or returned to the start. The counts are like
	/// <see cref="MemberDied"/>'s.
	/// </summary>
	public sealed record MemberUp(int Dead, int Of, bool OtherParty = false) : RunNote;

	/// <param name="Number">The run's deaths so far, this frame's included</param>
	public sealed record DeathsCounted(int Number) : RunNote;

	/// <summary>
	/// The run ended with more deaths than the site takes (<see cref="Run.MaxDeaths"/>), so it's sent without them
	/// </summary>
	public sealed record TooManyDeaths(int Deaths) : RunNote;

	/// <param name="Number">The run's wipes so far, this one included</param>
	/// <param name="Members">Everyone who had to die, for a wipe seen from the party</param>
	public sealed record Wiped(int Number, WipeSource Source, int Members) : RunNote;

	/// <summary>
	/// A wipe that's the same one already counted the other way
	/// </summary>
	/// <param name="After">How long after the one counted</param>
	public sealed record WipeSkipped(WipeSource Source, TimeSpan After) : RunNote;

	/// <summary>
	/// The game's wipe message while the player isn't in a run's duty: there's no run, or they're away from it
	/// </summary>
	public sealed record WipeIgnored : Ignored;

	/// <param name="WhileAway">It ended while the player was logged out, or before the plugin saw them back after starting</param>
	public sealed record Ended(Run Run, bool WhileAway) : RunNote;

	public sealed record Dropped(uint Duty, DropReason Reason) : RunNote;
}

public enum ClockStartReason
{
	/// <summary>
	/// "Duty commenced"
	/// </summary>
	DutyCommenced,

	/// <summary>
	/// Loaded in: a replacement, or back in the duty
	/// </summary>
	LoadedIn,

	/// <summary>
	/// The game says the duty has started, though "Duty commenced" wasn't seen
	/// </summary>
	DutyHadStarted,
}

public enum FightEnd
{
	NobodyInCombat,
	GameWipeMessage,
}

public enum DropReason
{
	/// <summary>
	/// The plugin was off for part of it, and didn't see it cleared
	/// </summary>
	PluginWasOff,

	/// <summary>
	/// The player never loaded in, so there's no job
	/// </summary>
	NeverLoadedIn,
}
