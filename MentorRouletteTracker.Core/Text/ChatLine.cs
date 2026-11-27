using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Text;

public static class ChatLine
{
	private const string Tag = "[MRT]";

	public static string Format(Run run, string dutyName, RunOutcome outcome, bool sayPace = true)
	{
		return $"{Tag} {Report(run, dutyName, outcome, sayPace)}";
	}

	public static string FormatWaiting(IReadOnlyList<LoggedRun> logged)
	{
		var runs = logged.Count == 1 ? "1 run that was waiting" : $"{logged.Count} runs that were waiting";
		var found = Milestones.Found(logged.Select(run => run.NewDuty).OfType<NewDuty>().ToList());
		var line = $"{Tag} Logged {runs}.";
		return found is null ? line : $"{line} {found}";
	}

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
