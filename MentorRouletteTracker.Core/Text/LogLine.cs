using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Text;

/// <summary>
/// The recorder's notes as lines for the plugin's log: duties and jobs by name and id, times like the site's, and party
/// members as counts only
/// </summary>
public static class LogLine
{
	/// <param name="duty">A duty's name, from its <c>ContentFinderCondition</c> id</param>
	/// <param name="job">A job's name, from its <c>ClassJob</c> id</param>
	public static string Format(RunNote note, Func<uint, string> duty, Func<uint, string> job)
	{
		string Duty(uint id) => $"{duty(id)} ({id})";

		return note switch
		{
			RunNote.Popped popped => $"Mentor Roulette pop, as a replacement: {TrueOrFalse(popped.Replacement)}",
			RunNote.PopIgnored => "A pop that isn't the Mentor Roulette: ignored",
			RunNote.Restored restored => $"A run in progress from before the plugin started: {Duty(restored.Duty)}"
				+ (restored.PluginWasOff ? ", logged only if it's cleared, since the plugin was off for part of it" : ""),
			RunNote.Started started => $"Run started: {Duty(started.Duty)}, replacement {TrueOrFalse(started.Replacement)}",
			RunNote.BackInRun back => $"Back in the run: {Duty(back.Duty)}",
			RunNote.ClockStarted clock => "Clock started: " + clock.Reason switch
			{
				ClockStartReason.DutyCommenced => "duty commenced",
				ClockStartReason.LoadedIn => "loaded in",
				_ => "the duty had started",
			},
			RunNote.JobKnown known => $"Job: {job(known.Job)} ({known.Job})",
			RunNote.LoggedOut loggedOut => $"Logged out: clock stopped at {Duration.Format(loggedOut.ClockSeconds)}",
			RunNote.PartyChanged party => PartyLine(party),
			RunNote.FightStarted => "Fight started",
			RunNote.FightOver { Reason: FightEnd.GameWipeMessage } => "Fight over: the game's wipe message",
			RunNote.FightOver => "Fight over: nobody alive is in combat",
			RunNote.MemberDied { OtherParty: false } died => $"A party member died: {died.Dead} of {died.Of} dead, " + DiedThisFight(died),
			RunNote.MemberDied died => $"A member of another party died: {died.Dead} of {died.Of} in the alliance dead, " + DiedThisFight(died),
			RunNote.MemberUp { OtherParty: false } up => $"A party member is up again: {up.Dead} of {up.Of} dead",
			RunNote.MemberUp up => $"A member of another party is up again: {up.Dead} of {up.Of} in the alliance dead",
			RunNote.DeathsCounted counted => $"Deaths this run: {counted.Number}",
			RunNote.TooManyDeaths tooMany => $"{Count(tooMany.Deaths, "death")} is more than the site takes ({Run.MaxDeaths}), so the run is sent without them",
			RunNote.Wiped { Source: WipeSource.Party } wiped => $"Wipe {wiped.Number}: everyone in the party died this fight ({wiped.Members} of {wiped.Members})",
			RunNote.Wiped wiped => $"Wipe {wiped.Number}: the game's wipe message",
			RunNote.WipeSkipped { Source: WipeSource.Party } skipped => $"Everyone in the party died this fight: already counted from the game's wipe message {Seconds(skipped.After)} ago",
			RunNote.WipeSkipped skipped => $"The game's wipe message: already counted from the party {Seconds(skipped.After)} ago",
			RunNote.WipeIgnored => "The game's wipe message, not in a run's duty: ignored",
			RunNote.Ended ended => EndedLine(ended, Duty(ended.Run.GameContentId)),
			RunNote.Dropped { Reason: DropReason.NeverLoadedIn } dropped => $"Run dropped: {Duty(dropped.Duty)}, never loaded in, so there's no job",
			RunNote.Dropped dropped => $"Run dropped: {Duty(dropped.Duty)}, the plugin was off for part of it and didn't see it cleared",
			_ => note.ToString(),
		};
	}

	private static string PartyLine(RunNote.PartyChanged party)
	{
		var line = $"Party: {Count(party.Members, "member")}";
		if (party.Alliance > 0)
		{
			line += $", alliance: {party.Alliance}";
		}

		return party.NotHere > 0 ? line + $", {party.NotHere} not here" : line;
	}

	private static string DiedThisFight(RunNote.MemberDied died)
	{
		return died.DiedThisFight is { } diedThisFight ? $"{diedThisFight} of {died.Of} have died this fight" : "outside a fight";
	}

	private static string EndedLine(RunNote.Ended ended, string duty)
	{
		var run = ended.Run;
		var how = run.Cleared ? "cleared" : ended.WhileAway ? "not cleared (not back after logging out or a crash)" : "not cleared (left)";
		var deaths = run.Deaths is { } count ? $", {Count(count, "death")}" : "";
		return $"Run ended: {duty}, {how}, {Count(run.Wipes, "wipe")}{deaths}, {Duration.Format(run.DurationSeconds)}";
	}

	private static string TrueOrFalse(bool value) => value ? "true" : "false";

	private static string Seconds(TimeSpan span) => Duration.Format((int)Math.Max(0, span.TotalSeconds));

	private static string Count(int count, string thing) => count == 1 ? $"1 {thing}" : $"{count} {thing}s";
}
