using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Text;

/// <summary>
/// The line in chat when a run ends: "[MRT] Logged #1234, The Vault, 32m 10s." The player just played it, so only the
/// duty and time say which run it was, and the rest is what happened to it. It reports, rather than telling the player
/// what to do.
/// </summary>
public static class ChatLine
{
	public static string Format(Run run, string dutyName, RunOutcome outcome)
	{
		return $"[MRT] {Report(run, dutyName, outcome)}";
	}

	private static string Report(Run run, string dutyName, RunOutcome outcome)
	{
		var what = $"{Names.Capitalize(dutyName)}, {Duration.Format(run.DurationSeconds)}";

		return outcome.Kind switch
		{
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
