using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker.Core.Tests;

public class ChatLineTests
{
	private static readonly Run Cleared = Runs.Make(hour: 1) with { Wipes = 2, DurationSeconds = 1930 };

	[Fact]
	public void ALoggedRun_SaysItsNumber_TheDutyAndTheTime()
	{
		Assert.Equal(
			"Logged #1234, The Vault, 32m 10s.",
			ChatLine.Format(Cleared, "the Vault", new RunOutcome(RunOutcomeKind.Logged, RunNumber: 1234)));
	}

	[Theory]
	[InlineData(RunOutcomeKind.NotConnected, "Saved The Vault, 32m 10s: not connected.")]
	[InlineData(RunOutcomeKind.AutoUploadOff, "Saved The Vault, 32m 10s: auto-upload is off.")]
	[InlineData(RunOutcomeKind.Waiting, "Failed to upload The Vault, 32m 10s.")]
	[InlineData(RunOutcomeKind.NeedsReconnect, "Failed to upload The Vault, 32m 10s: not connected.")]
	public void ARunNotUploaded_SaysWhy(RunOutcomeKind kind, string line)
	{
		Assert.Equal(line, ChatLine.Format(Cleared, "The Vault", new RunOutcome(kind)));
	}

	[Fact]
	public void ARefusedRun_SaysTheSitesReason()
	{
		Assert.Equal(
			"Failed to upload The Vault, 32m 10s: That duty isn't in the Mentor Roulette.",
			ChatLine.Format(Cleared, "The Vault", new RunOutcome(RunOutcomeKind.Refused, Reason: "That duty isn't in the Mentor Roulette")));
	}

	[Fact]
	public void TheJob_Clear_Wipes_AndReplacement_AreLeftOut()
	{
		var run = Cleared with { Cleared = false, Replacement = true, Wipes = 1, DurationSeconds = 242 };

		Assert.Equal(
			"Logged #7, The Vault, 4m 2s.",
			ChatLine.Format(run, "The Vault", new RunOutcome(RunOutcomeKind.Logged, RunNumber: 7)));
	}
}
