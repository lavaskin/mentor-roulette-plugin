using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Text;

/// <summary>
/// The line in chat when a run ends: "[MRT] Logged #1234, The Vault, 32m 10s." The player just played it, so only the
/// duty and time say which run it was, and the rest is what happened to it. It reports, rather than telling the player
/// what to do. A logged run then says how its time compares with the usual, and anything it found.
/// </summary>
public static class ChatLine
{
	private const string Tag = "[MRT]";

	/// <param name="sayPace">Say how its time compares with the usual, when the site said</param>
	public static string Format(Run run, string dutyName, RunOutcome outcome, bool sayPace = true)
	{
		return $"{Tag} {Report(run, dutyName, outcome, sayPace)}";
	}

	/// <summary>
	/// The line when runs that were waiting are logged, by any upload: "[MRT] Logged 3 runs that were waiting.", and
	/// what they found, if they found a new duty. A run that just ended isn't one of them, since it has its own line.
	/// </summary>
	/// <param name="logged">In the order they were logged</param>
	public static string FormatWaiting(IReadOnlyList<LoggedRun> logged)
	{
		var runs = logged.Count == 1 ? "1 run that was waiting" : $"{logged.Count} runs that were waiting";
		var found = Milestones.Found(logged.Select(run => run.NewDuty).OfType<NewDuty>().ToList());
		var line = $"{Tag} Logged {runs}.";
		return found is null ? line : $"{line} {found}";
	}

	/// <summary>
	/// The line when a run is dropped because the plugin was off for part of it: "[MRT] The Vault wasn't logged, since
	/// the plugin was off for part of it. You can log it on the site." Unlike the others, it says what the player can
	/// do, since nothing else tells them the run's missing.
	/// </summary>
	public static string FormatEndMissed(string dutyName)
	{
		return $"{Tag} {Names.Capitalize(dutyName)} wasn't logged, since the plugin was off for part of it. You can log it on the site.";
	}

	private static string Report(Run run, string dutyName, RunOutcome outcome, bool sayPace)
	{
		var what = $"{Names.Capitalize(dutyName)}, {Duration.Format(run.DurationSeconds)}";

		return outcome.Kind switch
		{
			RunOutcomeKind.Logged => Logged(outcome, what, sayPace),
			RunOutcomeKind.NotConnected => $"Saved {what}: not connected.",
			RunOutcomeKind.AutoUploadOff => $"Saved {what}: auto-upload is off.",
			RunOutcomeKind.Refused => $"Failed to upload {what}: {Sentence(outcome.Reason ?? "no reason given")}",
			RunOutcomeKind.NeedsReconnect => $"Failed to upload {what}: not connected.",
			_ => $"Failed to upload {what}.",
		};
	}

	private static string Logged(RunOutcome outcome, string what, bool sayPace)
	{
		var parts = new List<string> { $"Logged #{outcome.RunNumber}, {what}." };
		if (sayPace && outcome.Pace is { } pace)
		{
			parts.Add(Pace.Format(pace));
		}

		if (outcome.NewDuty is { } newDuty)
		{
			parts.Add(Milestones.Found(newDuty));
		}

		return string.Join(' ', parts);
	}

	private static string Sentence(string text)
	{
		text = text.Trim();
		return text.EndsWith('.') || text.EndsWith('!') || text.EndsWith('?') ? text : text + ".";
	}
}
