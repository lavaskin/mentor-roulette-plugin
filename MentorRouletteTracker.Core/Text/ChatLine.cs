using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Text;

/// <summary>
/// The line in chat when a run ends: what was recorded, and what happened to it.
/// "Mentor Roulette: The Vault, Paladin, cleared, 2 wipes, 32m 10s. Logged as run #1234."
/// </summary>
public static class ChatLine
{
	public static string Format(Run run, string dutyName, string jobName, RunOutcome outcome)
	{
		var parts = new List<string> { Names.Capitalize(dutyName), Names.Capitalize(jobName) };
		if (run.Replacement)
		{
			parts.Add("replacement");
		}

		parts.Add(run.Cleared ? "cleared" : "not cleared");
		parts.Add(run.Wipes == 1 ? "1 wipe" : $"{run.Wipes} wipes");
		parts.Add(Duration.Format(run.DurationSeconds));

		return $"Mentor Roulette: {string.Join(", ", parts)}. {Outcome(outcome)}";
	}

	private static string Outcome(RunOutcome outcome)
	{
		return outcome.Kind switch
		{
			RunOutcomeKind.Logged => $"Logged as run #{outcome.RunNumber}.",
			RunOutcomeKind.NotConnected => "Saved: connect to upload it.",
			RunOutcomeKind.AutoUploadOff => "Saved, not uploaded.",
			RunOutcomeKind.Refused => $"The site refused it: {Sentence(outcome.Reason ?? "no reason given")}",
			RunOutcomeKind.NeedsReconnect => "Saved: reconnect in settings to upload it.",
			_ => "Couldn't upload it yet: it'll try again later.",
		};
	}

	private static string Sentence(string text)
	{
		text = text.Trim();
		return text.EndsWith('.') || text.EndsWith('!') || text.EndsWith('?') ? text : text + ".";
	}
}
