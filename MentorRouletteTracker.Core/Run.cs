namespace MentorRouletteTracker.Core;

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
/// A finished Mentor Roulette run, kept forever in the local store whether it's uploaded or not.
/// </summary>
public sealed record Run
{
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
	/// The player's own time in the duty: whole seconds, at least 1
	/// </summary>
	public required int DurationSeconds { get; init; }

	/// <summary>
	/// When the run ended, in UTC
	/// </summary>
	public required DateTime DatePlayed { get; init; }

	public RunStatus Status { get; init; } = RunStatus.NotUploaded;

	/// <summary>
	/// The run's id on the site, for its link, once uploaded
	/// </summary>
	public int? SiteRunId { get; init; }

	/// <summary>
	/// The run's number on the site, once uploaded
	/// </summary>
	public int? RunNumber { get; init; }

	/// <summary>
	/// Why the site refused it, when <see cref="Status"/> is <see cref="RunStatus.Failed"/>
	/// </summary>
	public string? FailedReason { get; init; }
}
