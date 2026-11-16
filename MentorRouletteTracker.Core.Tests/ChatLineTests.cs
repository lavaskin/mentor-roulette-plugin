using MentorRouletteTracker.Core.Api;
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
			"[MRT] Logged #1234, The Vault, 32m 10s.",
			ChatLine.Format(Cleared, "the Vault", new RunOutcome(RunOutcomeKind.Logged, RunNumber: 1234)));
	}

	[Theory]
	[InlineData(RunOutcomeKind.NotConnected, "[MRT] Saved The Vault, 32m 10s: not connected.")]
	[InlineData(RunOutcomeKind.AutoUploadOff, "[MRT] Saved The Vault, 32m 10s: auto-upload is off.")]
	[InlineData(RunOutcomeKind.Waiting, "[MRT] Failed to upload The Vault, 32m 10s.")]
	[InlineData(RunOutcomeKind.NeedsReconnect, "[MRT] Failed to upload The Vault, 32m 10s: not connected.")]
	public void ARunNotUploaded_SaysWhy(RunOutcomeKind kind, string line)
	{
		Assert.Equal(line, ChatLine.Format(Cleared, "The Vault", new RunOutcome(kind)));
	}

	[Fact]
	public void ARefusedRun_SaysTheSitesReason()
	{
		Assert.Equal(
			"[MRT] Failed to upload The Vault, 32m 10s: That duty isn't in the Mentor Roulette.",
			ChatLine.Format(Cleared, "The Vault", new RunOutcome(RunOutcomeKind.Refused, Reason: "That duty isn't in the Mentor Roulette")));
	}

	[Fact]
	public void TheJob_Clear_Wipes_AndReplacement_AreLeftOut()
	{
		var run = Cleared with { Cleared = false, Replacement = true, Wipes = 1, DurationSeconds = 242 };

		Assert.Equal(
			"[MRT] Logged #7, The Vault, 4m 2s.",
			ChatLine.Format(run, "The Vault", new RunOutcome(RunOutcomeKind.Logged, RunNumber: 7)));
	}

	[Fact]
	public void ARunOnANewDuty_SaysSo()
	{
		Assert.Equal(
			"[MRT] Logged #1234, The Vault, 32m 10s. New duty found!",
			ChatLine.Format(Cleared, "The Vault", new RunOutcome(RunOutcomeKind.Logged, RunNumber: 1234, NewDuty: Found())));
	}

	[Fact]
	public void ARunThatFinishesAMilestone_GivesTheMilestone()
	{
		Assert.Equal(
			"[MRT] Logged #1234, The Vault, 32m 10s. Every Heavensward dungeon seen!",
			ChatLine.Format(Cleared, "The Vault", new RunOutcome(RunOutcomeKind.Logged, RunNumber: 1234, NewDuty: Found(ofTypeInExpansion: Done))));
	}

	[Theory]
	[InlineData(1, "[MRT] Logged 1 run that was waiting.")]
	[InlineData(3, "[MRT] Logged 3 runs that were waiting.")]
	public void RunsThatWereWaiting_SayHowManyWereLogged(int count, string line)
	{
		var logged = Enumerable.Range(1, count).Select(number => new LoggedRun(5000 + number, number)).ToList();

		Assert.Equal(line, ChatLine.FormatWaiting(logged));
	}

	[Fact]
	public void RunsThatWereWaiting_SayWhatTheyFound()
	{
		var logged = new List<LoggedRun>
		{
			new(5001, 1, Found()),
			new(5002, 2),
			new(5003, 3, Found(ofType: Done)),
		};

		Assert.Equal("[MRT] Logged 3 runs that were waiting. Every dungeon seen!", ChatLine.FormatWaiting(logged));
	}

	private static readonly SeenTotals Done = new(16, 16);

	private static NewDuty Found(SeenTotals? ofType = null, SeenTotals? ofTypeInExpansion = null) =>
		new(143, 186, ofType ?? new SeenTotals(70, 103), ofTypeInExpansion ?? new SeenTotals(10, 16), "Dungeon", "3.0: Heavensward");
}
