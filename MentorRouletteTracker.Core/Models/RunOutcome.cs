using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker.Core.Models;

public enum RunOutcomeKind
{
	/// <summary>
	/// Uploaded: the site logged it
	/// </summary>
	Logged,

	NotConnected,
	AutoUploadOff,

	/// <summary>
	/// The site couldn't be reached, so it'll try again later
	/// </summary>
	Waiting,

	/// <summary>
	/// The site refused it
	/// </summary>
	Refused,

	/// <summary>
	/// The site stopped accepting the token
	/// </summary>
	NeedsReconnect,
}

/// <summary>
/// What happened to a run when it ended, for the chat line
/// </summary>
/// <param name="NewDuty">When it's <see cref="RunOutcomeKind.Logged"/> and the user's first run on its duty</param>
public sealed record RunOutcome(RunOutcomeKind Kind, int? RunNumber = null, string? Reason = null, NewDuty? NewDuty = null);
