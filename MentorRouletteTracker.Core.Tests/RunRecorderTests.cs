namespace MentorRouletteTracker.Core.Tests;

public class RunRecorderTests
{
	private const uint Vault = 421;
	private const uint Sastasha = 4;
	private const uint Paladin = 19;

	private static readonly DateTime Start = new(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc);

	private readonly RunRecorder _recorder = new();

	private static DateTime At(int seconds) => Start.AddSeconds(seconds);

	/// <summary>
	/// A Mentor Roulette pop, arriving in the duty at 20s, loading in at 25s, and "Duty commenced" at 30s
	/// </summary>
	private void Enter(uint duty = Vault, int popAt = 0, int arriveAt = 20)
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(popAt));
		Assert.Null(_recorder.Arrived(duty, throughDutyFinder: true, At(arriveAt)));
		_recorder.LoadedIn(At(arriveAt + 5));
		_recorder.JobKnown(Paladin);
		Assert.True(_recorder.Commenced(At(arriveAt + 10)));
	}

	[Fact]
	public void AClearedRun_HasItsFieldsAndTime()
	{
		Enter();
		_recorder.Wiped();
		_recorder.Wiped();

		var run = _recorder.Completed(At(30 + 1930));

		Assert.NotNull(run);
		Assert.NotEqual(Guid.Empty, run.Id);
		Assert.Equal(Vault, run.GameContentId);
		Assert.Equal(Paladin, run.GameClassJobId);
		Assert.True(run.Cleared);
		Assert.False(run.Replacement);
		Assert.Equal(2, run.Wipes);
		Assert.Equal(1930, run.DurationSeconds);
		Assert.Equal(At(1960), run.DatePlayed);
		Assert.Equal(DateTimeKind.Utc, run.DatePlayed.Kind);
		Assert.Equal(RunStatus.NotUploaded, run.Status);
		Assert.False(_recorder.InDuty);
	}

	[Fact]
	public void TheTime_StartsAtDutyCommenced_LikeTheGames()
	{
		// The loading screen and the wait for the party to load in don't count: 27s, as the game says
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		Assert.False(_recorder.LoadedIn(At(23)));
		_recorder.Commenced(At(25));

		var run = _recorder.Completed(At(52));

		Assert.Equal(27, run!.DurationSeconds);
	}

	[Fact]
	public void DutyCommenced_OnlyStartsTheClockOnce()
	{
		Enter();
		Assert.False(_recorder.Commenced(At(100)));

		Assert.Equal(970, _recorder.Completed(At(1000))!.DurationSeconds);
	}

	[Fact]
	public void TheTime_EndsAtTheDutyCompleteMessage_NotOnLeaving()
	{
		Enter();
		var run = _recorder.Completed(At(630));

		// Loot and lingering afterwards don't count, and leaving finishes nothing more
		Assert.Null(_recorder.Arrived(0, throughDutyFinder: false, At(900)));
		Assert.Equal(600, run!.DurationSeconds);
	}

	[Fact]
	public void EachRun_GetsItsOwnPluginRunId()
	{
		Enter();
		var first = _recorder.Completed(At(100));
		_recorder.Arrived(0, false, At(110));
		Enter(popAt: 200, arriveAt: 230);
		var second = _recorder.Completed(At(400));

		Assert.NotEqual(first!.Id, second!.Id);
	}

	[Fact]
	public void TheTime_IsWholeSeconds_AtLeastOne()
	{
		Enter();
		var run = _recorder.Completed(At(30).AddMilliseconds(400));

		Assert.Equal(1, run!.DurationSeconds);
	}

	[Fact]
	public void Wipes_AreCounted()
	{
		Enter();
		for (var i = 0; i < 5; i++)
		{
			_recorder.Wiped();
		}

		Assert.Equal(5, _recorder.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void Wipes_OutsideARun_CountForNothing()
	{
		_recorder.Wiped();
		Enter();

		Assert.Equal(0, _recorder.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void ADeclinedOrWithdrawnPop_RecordsNothing()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));

		// Declined, and the queue's back to none, then the player teleports somewhere
		Assert.Null(_recorder.Arrived(0, throughDutyFinder: false, At(60)));
		Assert.False(_recorder.InDuty);
		Assert.Null(_recorder.Snapshot(At(60)));
	}

	[Fact]
	public void ADeclinedPop_ThenADutyEnteredAnotherWay_RecordsNothing()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));

		Assert.Null(_recorder.Arrived(Vault, throughDutyFinder: false, At(60)));
		Assert.False(_recorder.InDuty);
	}

	[Fact]
	public void APopThatExpired_RecordsNothing()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));

		_recorder.Arrived(Vault, throughDutyFinder: true, At(0) + RunRecorder.PopLifetime + TimeSpan.FromSeconds(1));

		Assert.False(_recorder.InDuty);
	}

	[Fact]
	public void APop_IsUsedOnce()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(0, throughDutyFinder: false, At(20));

		_recorder.Arrived(Vault, throughDutyFinder: true, At(40));

		Assert.False(_recorder.InDuty);
	}

	[Fact]
	public void AnotherRoulette_RecordsNothing()
	{
		_recorder.Popped(isMentorRoulette: false, inProgress: false, At(0));
		_recorder.Arrived(Sastasha, throughDutyFinder: true, At(30));
		_recorder.Commenced(At(40));

		Assert.False(_recorder.InDuty);
		Assert.Null(_recorder.Completed(At(900)));
	}

	[Fact]
	public void ADutyQueuedDirectly_AfterAMentorPop_RecordsNothing()
	{
		// The Mentor Roulette popped and was declined, then the player queued a duty directly, which popped
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Popped(isMentorRoulette: false, inProgress: false, At(60));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(90));

		Assert.False(_recorder.InDuty);
	}

	[Fact]
	public void ADutyEnteredWithoutAPop_RecordsNothing()
	{
		_recorder.Arrived(Vault, throughDutyFinder: true, At(0));
		_recorder.Commenced(At(10));

		Assert.False(_recorder.InDuty);
		Assert.Null(_recorder.Completed(At(900)));
	}

	[Fact]
	public void LeavingEarly_IsARunThatWasntCleared_WithItsWipesAndTime()
	{
		Enter();
		_recorder.Wiped();

		var run = _recorder.Arrived(0, throughDutyFinder: false, At(30 + 242));

		Assert.NotNull(run);
		Assert.False(run.Cleared);
		Assert.Equal(1, run.Wipes);
		Assert.Equal(242, run.DurationSeconds);
		Assert.Equal(At(272), run.DatePlayed);
	}

	[Fact]
	public void LeavingBeforeTheDutyCommenced_IsARunThatWasntCleared_OfOneSecond()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		_recorder.LoadedIn(At(25));

		var run = _recorder.Arrived(0, throughDutyFinder: false, At(200));

		Assert.False(run!.Cleared);
		Assert.Equal(1, run.DurationSeconds);
		Assert.Equal(At(200), run.DatePlayed);
	}

	[Fact]
	public void BeingKicked_IntoAnotherDuty_EndsTheRun()
	{
		Enter();

		var run = _recorder.Arrived(Sastasha, throughDutyFinder: false, At(500));

		Assert.False(run!.Cleared);
		Assert.Equal(Vault, run.GameContentId);
		Assert.False(_recorder.InDuty);
	}

	[Fact]
	public void AReplacement_IsMarked_AndItsTimeStartsOnceLoadedIn()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: true, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(90));
		_recorder.JobKnown(Paladin);
		Assert.True(_recorder.LoadedIn(At(95)));

		var run = _recorder.Completed(At(95 + 600));

		Assert.True(run!.Replacement);
		Assert.Equal(600, run.DurationSeconds);
	}

	[Fact]
	public void ZoneChanges_InsideTheDuty_DontEndIt()
	{
		Enter();

		Assert.Null(_recorder.Arrived(Vault, throughDutyFinder: true, At(300)));
		_recorder.LoadedIn(At(305));

		Assert.True(_recorder.InDuty);
		Assert.Equal(970, _recorder.Completed(At(1000))!.DurationSeconds);
	}

	[Fact]
	public void TheJob_IsTakenOnce()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(30));
		Assert.True(_recorder.NeedsJob);

		_recorder.JobKnown(Paladin);
		_recorder.JobKnown(24);

		Assert.False(_recorder.NeedsJob);
		Assert.Equal(Paladin, _recorder.Completed(At(100))!.GameClassJobId);
	}

	[Fact]
	public void ADisconnectAndReturn_IsOneRun_WithTheTimeAwayAndLoadingLeftOut()
	{
		Enter();
		_recorder.Wiped();
		_recorder.LoggedOut(At(330));
		Assert.True(_recorder.IsAway);

		// Back in the same duty ten minutes later, and loaded in 15 seconds after that
		Assert.Null(_recorder.Arrived(Vault, throughDutyFinder: true, At(930)));
		Assert.True(_recorder.InDuty);
		Assert.True(_recorder.LoadedIn(At(945)));
		_recorder.Wiped();
		var run = _recorder.Completed(At(1245));

		Assert.Equal(300 + 300, run!.DurationSeconds);
		Assert.Equal(2, run.Wipes);
		Assert.True(run.Cleared);
	}

	[Fact]
	public void ADisconnectBeforeTheDutyCommenced_StartsTheClockOnReturn()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		_recorder.LoggedOut(At(25));

		// It commenced while they were away, so there's no "Duty commenced" for them
		_recorder.Arrived(Vault, throughDutyFinder: true, At(200));
		_recorder.LoadedIn(At(210));

		Assert.Equal(100, _recorder.Completed(At(310))!.DurationSeconds);
	}

	[Fact]
	public void ADisconnect_WithoutReturning_IsNotCleared_AndEndsWhenTheyLeft()
	{
		Enter();
		_recorder.LoggedOut(At(330));

		var run = _recorder.Arrived(0, throughDutyFinder: false, At(2000));

		Assert.False(run!.Cleared);
		Assert.Equal(300, run.DurationSeconds);
		Assert.Equal(At(330), run.DatePlayed);
	}

	[Fact]
	public void TheClock_DoesntCountWhileLoggedOut_AndLoggingOutTwiceChangesNothing()
	{
		Enter();
		_recorder.LoggedOut(At(130));
		_recorder.LoggedOut(At(500));

		Assert.Null(_recorder.Completed(At(600)));
		Assert.False(_recorder.LoadedIn(At(600)));
		Assert.Equal(100, _recorder.Snapshot(At(600))!.ElapsedSeconds);
	}

	[Fact]
	public void ACrashAndRestart_StillInTheDuty_CarriesOn()
	{
		Enter();
		_recorder.Wiped();
		var saved = _recorder.Snapshot(At(30 + 600));

		// The game crashes; the plugin starts again, restores the saved run, and the player's back in the duty
		var restarted = new RunRecorder();
		restarted.Restore(saved!);
		Assert.True(restarted.IsAway);
		Assert.Null(restarted.Arrived(Vault, throughDutyFinder: true, At(30 + 900)));
		restarted.LoadedIn(At(30 + 900));
		restarted.Wiped();
		var run = restarted.Completed(At(30 + 1200));

		Assert.Equal(saved!.Id, run!.Id);
		Assert.Equal(600 + 300, run.DurationSeconds);
		Assert.Equal(2, run.Wipes);
		Assert.True(run.Cleared);
		Assert.Equal(Paladin, run.GameClassJobId);
	}

	[Fact]
	public void ACrashAndRestart_OutsideTheDuty_IsRecordedAsNotCleared()
	{
		Enter();
		_recorder.Wiped();
		var saved = _recorder.Snapshot(At(30 + 600));

		var restarted = new RunRecorder();
		restarted.Restore(saved!);
		var run = restarted.Arrived(0, throughDutyFinder: false, At(5000));

		Assert.NotNull(run);
		Assert.Equal(saved!.Id, run.Id);
		Assert.False(run.Cleared);
		Assert.Equal(1, run.Wipes);
		Assert.Equal(600, run.DurationSeconds);
		Assert.Equal(At(630), run.DatePlayed);
		Assert.False(restarted.IsAway);
	}

	[Fact]
	public void TheSnapshot_CountsTheTimeUpToNow()
	{
		Enter();
		_recorder.Wiped();

		var snapshot = _recorder.Snapshot(At(90));

		Assert.Equal(60, snapshot!.ElapsedSeconds);
		Assert.Equal(At(90), snapshot.LastSeenAt);
		Assert.Equal(1, snapshot.Wipes);
		Assert.Equal(Vault, snapshot.GameContentId);
		Assert.Equal(Paladin, snapshot.GameClassJobId);
		Assert.True(_recorder.InDuty);
	}

	[Fact]
	public void TheSnapshot_BeforeTheDutyCommenced_HasNoTime()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));

		var snapshot = _recorder.Snapshot(At(25));

		Assert.Equal(0, snapshot!.ElapsedSeconds);
		Assert.Equal(At(25), snapshot.LastSeenAt);
	}

	[Fact]
	public void TheSnapshot_WhileLoggedOut_IsWhenTheyLeft()
	{
		Enter();
		_recorder.LoggedOut(At(90));

		var snapshot = _recorder.Snapshot(At(500));

		Assert.Equal(60, snapshot!.ElapsedSeconds);
		Assert.Equal(At(90), snapshot.LastSeenAt);
	}

	[Fact]
	public void ThereIsNoSnapshot_OnceTheRunEnds()
	{
		Enter();
		_recorder.Completed(At(100));

		Assert.Null(_recorder.Snapshot(At(100)));
	}
}
