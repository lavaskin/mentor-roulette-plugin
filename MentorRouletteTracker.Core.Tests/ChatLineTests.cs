using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker.Core.Tests;

public class ChatLineTests
{
	private static readonly Run Cleared = Runs.Make(hour: 1) with { Wipes = 2, DurationSeconds = 1930 };
	private static readonly Run Left = Runs.Make(hour: 1) with { Cleared = false, Wipes = 0, DurationSeconds = 242 };

	[Fact]
	public void ALoggedRun_SaysItsNumber()
	{
		Assert.Equal(
			"Mentor Roulette: The Vault, Paladin, cleared, 2 wipes, 32m 10s. Logged as run #1234.",
			ChatLine.Format(Cleared, "the Vault", "paladin", new RunOutcome(RunOutcomeKind.Logged, RunNumber: 1234)));
	}

	[Theory]
	[InlineData(RunOutcomeKind.NotConnected, "Saved: connect to upload it.")]
	[InlineData(RunOutcomeKind.AutoUploadOff, "Saved, not uploaded.")]
	[InlineData(RunOutcomeKind.Waiting, "Couldn't upload it yet: it'll try again later.")]
	[InlineData(RunOutcomeKind.NeedsReconnect, "Saved: reconnect in settings to upload it.")]
	public void ARunNotUploaded_SaysWhy(RunOutcomeKind kind, string ending)
	{
		Assert.Equal(
			"Mentor Roulette: The Vault, Paladin, not cleared, 0 wipes, 4m 2s. " + ending,
			ChatLine.Format(Left, "The Vault", "Paladin", new RunOutcome(kind)));
	}

	[Fact]
	public void ARefusedRun_SaysTheSitesReason()
	{
		Assert.EndsWith(
			"The site refused it: That duty isn't in the Mentor Roulette.",
			ChatLine.Format(Cleared, "The Vault", "Paladin", new RunOutcome(RunOutcomeKind.Refused, Reason: "That duty isn't in the Mentor Roulette")));
	}

	[Fact]
	public void AReplacement_AndOneWipe_ReadRight()
	{
		var run = Cleared with { Replacement = true, Wipes = 1 };

		Assert.StartsWith(
			"Mentor Roulette: The Vault, Paladin, replacement, cleared, 1 wipe, 32m 10s.",
			ChatLine.Format(run, "The Vault", "Paladin", new RunOutcome(RunOutcomeKind.AutoUploadOff)));
	}
}
