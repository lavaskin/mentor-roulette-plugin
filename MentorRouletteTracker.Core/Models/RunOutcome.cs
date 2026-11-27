using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker.Core.Models;

public enum RunOutcomeKind
{
	Logged,

	NotConnected,
	AutoUploadOff,

	Waiting,

	Refused,

	NeedsReconnect,
}

public sealed record RunOutcome(RunOutcomeKind Kind, int? RunNumber = null, string? Reason = null, NewDuty? NewDuty = null, RunPace? Pace = null);
