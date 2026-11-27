using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker.Core.Tests;

public class LogLineTests
{
	private const uint Gauntlet = 737;
	private const uint Paladin = 19;

	private static string Line(RunNote note)
	{
		return LogLine.Format(
			note,
			duty => duty == Gauntlet ? "The Heroes' Gauntlet" : "Unknown duty",
			job => job == Paladin ? "Paladin" : "Unknown job");
	}

	private static Run Run(bool cleared, int wipes, int seconds) => new()
	{
		Id = Guid.NewGuid(),
		GameContentId = Gauntlet,
		GameClassJobId = Paladin,
		Cleared = cleared,
		Replacement = false,
		Wipes = wipes,
		DurationSeconds = seconds,
		DatePlayed = DateTime.UtcNow,
	};

	public static TheoryData<RunNote, string> Lines => new()
	{
		{ new RunNote.Popped(Replacement: false), "Mentor Roulette pop, as a replacement: no" },
		{ new RunNote.Started(Gauntlet, Replacement: true), "Run started: The Heroes' Gauntlet (737), replacement yes" },
		{ new RunNote.ClockStarted(ClockStartReason.DutyCommenced), "Clock started: duty commenced" },
		{ new RunNote.ClockStarted(ClockStartReason.LoadedIn), "Clock started: loaded in" },
		{ new RunNote.ClockStarted(ClockStartReason.DutyHadStarted), "Clock started: the duty had started" },
		{ new RunNote.JobKnown(Paladin), "Job: Paladin (19)" },
		{ new RunNote.PartyChanged(4, 0, 0), "Party: 4 members" },
		{ new RunNote.PartyChanged(1, 0, 0), "Party: 1 member" },
		{ new RunNote.PartyChanged(8, 24, 0), "Party: 8 members, alliance: 24" },
		{ new RunNote.PartyChanged(3, 0, 1), "Party: 3 members, 1 not here" },
		{ new RunNote.FightStarted(), "Fight started" },
		{ new RunNote.FightOver(FightEnd.NobodyInCombat), "Fight over: nobody alive is in combat" },
		{ new RunNote.FightOver(FightEnd.GameWipeMessage), "Fight over: the game's wipe message" },
		{ new RunNote.MemberDied(2, 3, 4), "A party member died: 2 of 4 dead, 3 of 4 have died this fight" },
		{ new RunNote.MemberDied(1, null, 4), "A party member died: 1 of 4 dead, outside a fight" },
		{ new RunNote.MemberUp(1, 4), "A party member is up again: 1 of 4 dead" },
		{ new RunNote.Wiped(1, WipeSource.Party, 4), "Wipe 1: everyone in the party died this fight (4 of 4)" },
		{ new RunNote.Wiped(2, WipeSource.GameMessage, 0), "Wipe 2: the game's wipe message" },
		{ new RunNote.WipeSkipped(WipeSource.GameMessage, TimeSpan.FromSeconds(3.4)), "The game's wipe message: already counted from the party 3s ago" },
		{ new RunNote.WipeSkipped(WipeSource.Party, TimeSpan.FromSeconds(12)), "Everyone in the party died this fight: already counted from the game's wipe message 12s ago" },
		{ new RunNote.LoggedOut(723), "Logged out: clock stopped at 12m 3s" },
		{ new RunNote.BackInRun(Gauntlet), "Back in the run: The Heroes' Gauntlet (737)" },
		{ new RunNote.Ended(Run(cleared: true, wipes: 1, seconds: 1102), WhileAway: false), "Run ended: The Heroes' Gauntlet (737), cleared, 1 wipe, 18m 22s" },
		{ new RunNote.Ended(Run(cleared: false, wipes: 2, seconds: 300), WhileAway: false), "Run ended: The Heroes' Gauntlet (737), not cleared (left), 2 wipes, 5m" },
		{ new RunNote.Ended(Run(cleared: false, wipes: 0, seconds: 300), WhileAway: true), "Run ended: The Heroes' Gauntlet (737), not cleared (not back after logging out or a crash), 0 wipes, 5m" },
		{ new RunNote.Dropped(Gauntlet, DropReason.NeverLoadedIn), "Run dropped: The Heroes' Gauntlet (737), never loaded in, so there's no job" },
		{ new RunNote.Dropped(Gauntlet, DropReason.PluginWasOff), "Run dropped: The Heroes' Gauntlet (737), the plugin was off for part of it and didn't see it cleared" },
		{ new RunNote.Restored(Gauntlet, PluginWasOff: false), "A run in progress from before the plugin started: The Heroes' Gauntlet (737)" },
		{ new RunNote.PopIgnored(), "A pop that isn't the Mentor Roulette: ignored" },
		{ new RunNote.WipeIgnored(), "The game's wipe message, with no run in progress: ignored" },
	};

	[Theory]
	[MemberData(nameof(Lines))]
	public void EachNote_HasItsLine(RunNote note, string expected)
	{
		Assert.Equal(expected, Line(note));
	}

	[Fact]
	public void EveryKindOfNote_HasALineOfItsOwn()
	{
		// A note without a case would fall back to the record's own text, naming its type
		var kinds = typeof(RunNote).GetNestedTypes().Where(type => type.IsSubclassOf(typeof(RunNote)));
		var covered = Lines.Select(row => ((RunNote)row[0]).GetType()).ToHashSet();

		Assert.All(kinds, kind => Assert.Contains(kind, covered));
		Assert.All(Lines, row => Assert.DoesNotContain(nameof(RunNote), Line(((RunNote)row[0]))));
	}
}
