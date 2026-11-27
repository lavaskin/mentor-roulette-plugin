namespace MentorRouletteTracker.Core.Models;

public enum RunStatus
{
	NotUploaded,
	Uploaded,

	/// <summary>
	/// The site refused it (a 400). It's set aside, so it doesn't hold up later runs, until it's retried.
	/// </summary>
	Failed,
}

/// <summary>
/// A finished Mentor Roulette run, kept in the local store whether it's uploaded or not, until the user deletes it.
/// </summary>
public sealed record Run
{
	/// <summary>
	/// The most deaths the site takes. A run with more is sent without them.
	/// </summary>
	public const int MaxDeaths = 999;

	/// <summary>
	/// The plugin run id: made for each run, so a retried upload can't log it twice
	/// </summary>
	public required Guid Id { get; init; }

	/// <summary>
	/// The game's <c>ContentFinderCondition</c> row id
	/// </summary>
	public required uint GameContentId { get; init; }

	/// <summary>
	/// The game's <c>ClassJob</c> row id
	/// </summary>
	public required uint GameClassJobId { get; init; }

	public required bool Cleared { get; init; }

	/// <summary>
	/// Joined a duty in progress
	/// </summary>
	public required bool Replacement { get; init; }

	public required int Wipes { get; init; }

	/// <summary>
	/// The deaths of everyone in the duty, the player included: 0 to <see cref="MaxDeaths"/>. Null in a run recorded
	/// before the plugin counted them, or with more than the site takes. Not <c>required</c>, so the runs those
	/// versions saved still read.
	/// </summary>
	public int? Deaths { get; init; }

	/// <summary>
	/// The player's own time in the duty: whole seconds, at least 1
	/// </summary>
	public required int DurationSeconds { get; init; }

	/// <summary>
	/// When the player entered the duty, in UTC, so a run that goes past midnight counts for the day it started. Runs
	/// recorded by older versions of the plugin are dated when they ended, and keep those dates.
	/// </summary>
	public required DateTime DatePlayed { get; init; }

	public RunStatus Status { get; init; } = RunStatus.NotUploaded;

	/// <summary>
	/// The run's id on the site, for its link, once uploaded
	/// </summary>
	public long? SiteRunId { get; init; }

	/// <summary>
	/// The run's number on the site, once uploaded
	/// </summary>
	public int? RunNumber { get; init; }

	/// <summary>
	/// Why the site refused it, when <see cref="Status"/> is <see cref="RunStatus.Failed"/>
	/// </summary>
	public string? FailedReason { get; init; }
}
