namespace MentorRouletteTracker.Core.Models;

public enum RunStatus
{
	NotUploaded,
	Uploaded,

	Failed,
}

public sealed record Run
{
	public const int MaxDeaths = 999;

	public required Guid Id { get; init; }

	public required uint GameContentId { get; init; }

	public required uint GameClassJobId { get; init; }

	public required bool Cleared { get; init; }

	public required bool Replacement { get; init; }

	public required int Wipes { get; init; }

	public int? Deaths { get; init; }

	public required int DurationSeconds { get; init; }

	public required DateTime DatePlayed { get; init; }

	public RunStatus Status { get; init; } = RunStatus.NotUploaded;

	public long? SiteRunId { get; init; }

	public int? RunNumber { get; init; }

	public string? FailedReason { get; init; }
}
