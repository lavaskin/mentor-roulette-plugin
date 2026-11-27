namespace MentorRouletteTracker.Core.Models;

public sealed record InProgressRun
{
	public required Guid Id { get; init; }
	public required uint GameContentId { get; init; }

	public required uint GameClassJobId { get; init; }

	public required bool Replacement { get; init; }
	public required int Wipes { get; init; }

	public int? Deaths { get; init; }

	public required double ElapsedSeconds { get; init; }

	public DateTime? EnteredAt { get; init; }

	public required DateTime LastSeenAt { get; init; }

	public bool AwaitingDutyCommenced { get; init; }

	public bool PluginTurnedOff { get; init; }

	public bool ClockRanWhileOff { get; init; }
}
