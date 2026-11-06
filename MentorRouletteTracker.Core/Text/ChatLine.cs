using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Text;

/// <summary>
/// The line in chat when a run ends: "Logged #1234, The Vault, 32m 10s." The player just played it, so only the duty
/// and time say which run it was, and the rest is what happened to it.
/// </summary>
public static class ChatLine
{
	public static string Format(Run run, string dutyName, RunOutcome outcome)
	{
		var what = $"{Names.Capitalize(dutyName)}, {Duration.Format(run.DurationSeconds)}";

		return outcome.Kind switch
		{
			RunOutcomeKind.Logged => $"Logged #{outcome.RunNumber}, {what}.",
			RunOutcomeKind.NotConnected => $"Saved {what}: connect to upload it.",
			RunOutcomeKind.AutoUploadOff => $"Saved {what}, not uploaded.",
			RunOutcomeKind.Refused => $"Couldn't log {what}: {Sentence(outcome.Reason ?? "no reason given")}",
			RunOutcomeKind.NeedsReconnect => $"Saved {what}: reconnect in settings to upload it.",
			_ => $"Saved {what}: couldn't upload it yet, it'll try again later.",
		};
	}

	private static string Sentence(string text)
	{
		text = text.Trim();
		return text.EndsWith('.') || text.EndsWith('!') || text.EndsWith('?') ? text : text + ".";
	}
}
