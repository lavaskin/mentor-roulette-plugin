namespace MentorRouletteTracker.Core;

/// <summary>
/// The run in progress as it's saved to disk, so a crash loses at most the time since it was last saved. When the
/// plugin starts again it carries on if the player is back in the duty, and otherwise ends as not cleared.
/// </summary>
public sealed record InProgressRun
{
	public required Guid Id { get; init; }
	public required uint GameContentId { get; init; }

	/// <summary>
	/// 0 until the plugin has read the player's job
	/// </summary>
	public required uint GameClassJobId { get; init; }

	public required bool Replacement { get; init; }
	public required int Wipes { get; init; }

	/// <summary>
	/// The player's time in the duty up to <see cref="LastSeenAt"/>
	/// </summary>
	public required double ElapsedSeconds { get; init; }

	/// <summary>
	/// The last time the player was known to be in the duty, in UTC: when this was saved, or when they logged out
	/// </summary>
	public required DateTime LastSeenAt { get; init; }
}
