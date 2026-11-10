using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Text;

/// <summary>
/// The line in chat when a run ends: "[MRT] Logged #1234, The Vault, 32m 10s." The player just played it, so only the
/// duty and time say which run it was, and the rest is what happened to it. It reports, rather than telling the player
/// what to do.
/// </summary>
public static class ChatLine
{
	private const string Tag = "[MRT]";

	public static string Format(Run run, string dutyName, RunOutcome outcome)
	{
		return $"{Tag} {Report(run, dutyName, outcome)}";
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

	private static string Report(Run run, string dutyName, RunOutcome outcome)
	{
		var what = $"{Names.Capitalize(dutyName)}, {Duration.Format(run.DurationSeconds)}";

		return outcome.Kind switch
		{
			RunOutcomeKind.Logged when outcome.NewDuty is { } newDuty => $"Logged #{outcome.RunNumber}, {what}. {Milestones.Found(newDuty)}",
			RunOutcomeKind.Logged => $"Logged #{outcome.RunNumber}, {what}.",
			RunOutcomeKind.NotConnected => $"Saved {what}: not connected.",
			RunOutcomeKind.AutoUploadOff => $"Saved {what}: auto-upload is off.",
			RunOutcomeKind.Refused => $"Failed to upload {what}: {Sentence(outcome.Reason ?? "no reason given")}",
			RunOutcomeKind.NeedsReconnect => $"Failed to upload {what}: not connected.",
			_ => $"Failed to upload {what}.",
		};
	}

	private static string Sentence(string text)
	{
		text = text.Trim();
		return text.EndsWith('.') || text.EndsWith('!') || text.EndsWith('?') ? text : text + ".";
	}
}
