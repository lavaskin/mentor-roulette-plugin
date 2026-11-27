namespace MentorRouletteTracker.Core.Models;

public abstract record RunNote
{
	public abstract record Ignored : RunNote;

	public sealed record Popped(bool Replacement) : RunNote;

	public sealed record PopIgnored : Ignored;

	public sealed record Restored(uint Duty, bool PluginWasOff) : RunNote;

	public sealed record Started(uint Duty, bool Replacement) : RunNote;

	public sealed record BackInRun(uint Duty) : RunNote;

	public sealed record ClockStarted(ClockStartReason Reason) : RunNote;

	public sealed record JobKnown(uint Job) : RunNote;

	public sealed record LoggedOut(int ClockSeconds) : RunNote;

	public sealed record PartyChanged(int Members, int Alliance, int NotHere) : RunNote;

	public sealed record FightStarted : RunNote;

	public sealed record FightOver(FightEnd Reason) : RunNote;

	public sealed record MemberDied(int Dead, int? DiedThisFight, int Of, bool OtherParty = false) : RunNote;

	public sealed record MemberUp(int Dead, int Of, bool OtherParty = false) : RunNote;

	public sealed record DeathsCounted(int Number) : RunNote;

	public sealed record TooManyDeaths(int Deaths) : RunNote;

	public sealed record Wiped(int Number, WipeSource Source, int Members) : RunNote;

	public sealed record WipeSkipped(WipeSource Source, TimeSpan After) : RunNote;

	public sealed record WipeIgnored : Ignored;

	public sealed record Ended(Run Run, bool WhileAway) : RunNote;

	public sealed record Dropped(uint Duty, DropReason Reason) : RunNote;
}

public enum ClockStartReason
{
	DutyCommenced,

	LoadedIn,

	DutyHadStarted,
}

public enum FightEnd
{
	NobodyInCombat,
	GameWipeMessage,
}

public enum DropReason
{
	PluginWasOff,

	NeverLoadedIn,
}
