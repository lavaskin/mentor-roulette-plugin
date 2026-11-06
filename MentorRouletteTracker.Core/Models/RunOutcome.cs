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
public sealed record RunOutcome(RunOutcomeKind Kind, int? RunNumber = null, string? Reason = null);
