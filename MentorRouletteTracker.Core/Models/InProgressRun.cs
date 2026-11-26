namespace MentorRouletteTracker.Core.Models;

/// <summary>
/// The run in progress as it's saved to disk, so a crash loses at most the time since it was last saved. When the
/// plugin starts again it carries on if the player is back in the duty, and otherwise ends as not cleared (or is
/// dropped, if <see cref="PluginTurnedOff"/>).
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
	/// When the player entered the duty, in UTC: the run's date played. Null in a run saved by a version that didn't
	/// keep it, which is dated at its end minus its time instead.
	/// </summary>
	public DateTime? EnteredAt { get; init; }

	/// <summary>
	/// The last time the player was known to be in the duty, in UTC: when this was saved, or when they logged out
	/// </summary>
	public required DateTime LastSeenAt { get; init; }

	/// <summary>
	/// The clock hasn't started, and starts at "Duty commenced"
	/// </summary>
	public bool AwaitingDutyCommenced { get; init; }

	/// <summary>
	/// The plugin was turned off, reloaded or updated during the run (not the game closing), so it may have missed
	/// how the run ended. It's logged only if the plugin sees the clear itself.
	/// </summary>
	public bool PluginTurnedOff { get; init; }

	/// <summary>
	/// The clock was running when the plugin was turned off, so if the player's in the duty when it's back, the time
	/// in between counts: they were in the duty all along
	/// </summary>
	public bool ClockRanWhileOff { get; init; }
}
