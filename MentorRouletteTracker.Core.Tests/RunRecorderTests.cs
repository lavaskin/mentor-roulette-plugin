using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Recording;

namespace MentorRouletteTracker.Core.Tests;

public class RunRecorderTests
{
	private const uint Vault = 421;
	private const uint Sastasha = 4;
	private const uint Paladin = 19;

	private static readonly DateTime Start = new(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc);

	private readonly RunRecorder _recorder = new();

	private static DateTime At(int seconds) => Start.AddSeconds(seconds);

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
		_recorder.GameWiped(At(100));
		_recorder.GameWiped(At(200));

		var run = _recorder.Completed(At(30 + 1930));

		Assert.NotNull(run);
		Assert.NotEqual(Guid.Empty, run.Id);
		Assert.Equal(Vault, run.GameContentId);
		Assert.Equal(Paladin, run.GameClassJobId);
		Assert.True(run.Cleared);
		Assert.False(run.Replacement);
		Assert.Equal(2, run.Wipes);
		Assert.Equal(1930, run.DurationSeconds);
		Assert.Equal(At(20), run.DatePlayed);
		Assert.Equal(DateTimeKind.Utc, run.DatePlayed.Kind);
		Assert.Equal(RunStatus.NotUploaded, run.Status);
		Assert.False(_recorder.InDuty);
	}

	[Fact]
	public void TheTime_StartsAtDutyCommenced_LikeTheGames()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		Assert.False(_recorder.LoadedIn(At(23)));
		_recorder.JobKnown(Paladin);
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
			_recorder.GameWiped(At(100 + i * 60));
		}

		Assert.Equal(5, _recorder.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void Wipes_OutsideARun_CountForNothing()
	{
		_recorder.GameWiped(At(0));
		Enter();

		Assert.Equal(0, _recorder.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void ADeclinedOrWithdrawnPop_RecordsNothing()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));

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
	public void APopTheRecorderMissed_IsTakenFromTheDutyFinderOnArrival()
	{
		var run = new RunRecorder();
		run.Arrived(Vault, throughDutyFinder: true, At(20), new AcceptedPop(IsMentorRoulette: true, InProgress: false));
		run.JobKnown(Paladin);
		run.Commenced(At(30));

		var finished = run.Completed(At(130));

		Assert.Equal(Vault, finished!.GameContentId);
		Assert.False(finished.Replacement);
		Assert.Equal(100, finished.DurationSeconds);
		Assert.Equal(At(20), finished.DatePlayed);
	}

	[Fact]
	public void APopTakenFromTheDutyFinder_CanBeAReplacement()
	{
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20), new AcceptedPop(IsMentorRoulette: true, InProgress: true));
		_recorder.LoadedIn(At(25));
		_recorder.JobKnown(Paladin);

		var run = _recorder.Completed(At(125));

		Assert.True(run!.Replacement);
		Assert.Equal(100, run.DurationSeconds);
	}

	[Fact]
	public void AnotherRoulette_InTheDutyFinder_RecordsNothing()
	{
		_recorder.Arrived(Sastasha, throughDutyFinder: true, At(20), new AcceptedPop(IsMentorRoulette: false, InProgress: false));

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
		_recorder.GameWiped(At(100));

		var run = _recorder.Arrived(0, throughDutyFinder: false, At(30 + 242));

		Assert.NotNull(run);
		Assert.False(run.Cleared);
		Assert.Equal(1, run.Wipes);
		Assert.Equal(242, run.DurationSeconds);
		Assert.Equal(At(20), run.DatePlayed);
	}

	[Fact]
	public void LeavingBeforeTheDutyCommenced_IsARunThatWasntCleared_OfOneSecond()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		_recorder.LoadedIn(At(25));
		_recorder.JobKnown(Paladin);

		var run = _recorder.Arrived(0, throughDutyFinder: false, At(200));

		Assert.False(run!.Cleared);
		Assert.Equal(1, run.DurationSeconds);
		Assert.Equal(At(20), run.DatePlayed);
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
		Assert.Equal(At(90), run.DatePlayed);
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
		_recorder.GameWiped(At(100));
		_recorder.LoggedOut(At(330));
		Assert.True(_recorder.IsAway);

		Assert.Null(_recorder.Arrived(Vault, throughDutyFinder: true, At(930)));
		Assert.True(_recorder.InDuty);
		Assert.True(_recorder.LoadedIn(At(945)));
		_recorder.GameWiped(At(1000));
		var run = _recorder.Completed(At(1245));

		Assert.Equal(300 + 300, run!.DurationSeconds);
		Assert.Equal(2, run.Wipes);
		Assert.True(run.Cleared);
		Assert.Equal(At(20), run.DatePlayed);
	}

	[Fact]
	public void ADisconnectBeforeTheDutyCommenced_StartsTheClockOnReturn()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		_recorder.LoggedOut(At(25));

		_recorder.Arrived(Vault, throughDutyFinder: true, At(200));
		_recorder.LoadedIn(At(210));
		_recorder.JobKnown(Paladin);

		Assert.Equal(100, _recorder.Completed(At(310))!.DurationSeconds);
	}

	[Fact]
	public void ADisconnectOnTheLoadingScreen_WithoutReturning_RecordsNothing()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		_recorder.LoggedOut(At(22));

		Assert.Null(_recorder.Arrived(0, throughDutyFinder: false, At(500)));
		Assert.False(_recorder.IsAway);
		Assert.Null(_recorder.Snapshot(At(500)));
	}

	[Fact]
	public void ACrashOnTheLoadingScreen_WithoutReturning_RecordsNothing()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		var saved = _recorder.Snapshot(At(21));

		var restarted = new RunRecorder();
		restarted.Restore(saved!);

		Assert.Null(restarted.Arrived(0, throughDutyFinder: false, At(500)));
		Assert.False(restarted.IsAway);
	}

	[Fact]
	public void ADisconnect_WithoutReturning_IsNotCleared_AndEndsWhenTheyLeft()
	{
		Enter();
		_recorder.LoggedOut(At(330));

		var run = _recorder.Arrived(0, throughDutyFinder: false, At(2000));

		Assert.False(run!.Cleared);
		Assert.Equal(300, run.DurationSeconds);
		Assert.Equal(At(20), run.DatePlayed);
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
		_recorder.GameWiped(At(100));
		var saved = _recorder.Snapshot(At(30 + 600));

		var restarted = new RunRecorder();
		restarted.Restore(saved!);
		Assert.True(restarted.IsAway);
		Assert.Null(restarted.Arrived(Vault, throughDutyFinder: true, At(30 + 900)));
		restarted.LoadedIn(At(30 + 900));
		restarted.GameWiped(At(30 + 1000));
		var run = restarted.Completed(At(30 + 1200));

		Assert.Equal(saved!.Id, run!.Id);
		Assert.Equal(600 + 300, run.DurationSeconds);
		Assert.Equal(2, run.Wipes);
		Assert.True(run.Cleared);
		Assert.Equal(Paladin, run.GameClassJobId);
		Assert.Equal(At(20), run.DatePlayed);
	}

	[Fact]
	public void ARunSavedWithoutItsEntryTime_IsDatedAtItsEndMinusItsTime()
	{
		Enter();
		var saved = _recorder.Snapshot(At(30 + 600))! with { EnteredAt = null };

		var restarted = new RunRecorder();
		restarted.Restore(saved);
		restarted.Arrived(Vault, throughDutyFinder: true, At(30 + 900));
		restarted.LoadedIn(At(30 + 900));
		var run = restarted.Completed(At(30 + 1200));

		Assert.Equal(600 + 300, run!.DurationSeconds);
		Assert.Equal(At(30 + 1200 - 900), run.DatePlayed);
		Assert.Equal(DateTimeKind.Utc, run.DatePlayed.Kind);
	}

	[Fact]
	public void ARunSavedWithoutItsEntryTime_ThatEndedWhileAway_IsDatedWhenLastSeenMinusItsTime()
	{
		Enter();
		var saved = _recorder.Snapshot(At(30 + 600))! with { EnteredAt = null };

		var restarted = new RunRecorder();
		restarted.Restore(saved);
		var run = restarted.Arrived(0, throughDutyFinder: false, At(5000));

		Assert.Equal(600, run!.DurationSeconds);
		Assert.Equal(At(30), run.DatePlayed);
	}

	[Fact]
	public void AReloadBeforeTheDutyCommenced_StillWaitsForIt()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		_recorder.LoadedIn(At(25));
		_recorder.JobKnown(Paladin);
		var saved = _recorder.Snapshot(At(26));
		Assert.True(saved!.AwaitingDutyCommenced);

		var restarted = new RunRecorder();
		restarted.Restore(saved);
		restarted.Arrived(Vault, throughDutyFinder: true, At(28));
		Assert.False(restarted.LoadedIn(At(28)));
		Assert.True(restarted.Commenced(At(40)));

		Assert.Equal(100, restarted.Completed(At(140))!.DurationSeconds);
	}

	[Fact]
	public void AReloadAfterTheDutyCommenced_CarriesOnOnceLoadedIn()
	{
		Enter();
		var saved = _recorder.Snapshot(At(130));
		Assert.False(saved!.AwaitingDutyCommenced);

		var restarted = new RunRecorder();
		restarted.Restore(saved);
		restarted.Arrived(Vault, throughDutyFinder: true, At(140));

		Assert.True(restarted.LoadedIn(At(145)));
		Assert.Equal(100 + 55, restarted.Completed(At(200))!.DurationSeconds);
	}

	[Fact]
	public void AReplacementReloadedBeforeLoadingIn_CarriesOnOnceLoadedIn()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: true, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		var saved = _recorder.Snapshot(At(21));
		Assert.False(saved!.AwaitingDutyCommenced);

		var restarted = new RunRecorder();
		restarted.Restore(saved);
		restarted.Arrived(Vault, throughDutyFinder: true, At(30));
		restarted.JobKnown(Paladin);

		Assert.True(restarted.LoadedIn(At(35)));
		Assert.Equal(100, restarted.Completed(At(135))!.DurationSeconds);
	}

	[Fact]
	public void ACrashAndRestart_OutsideTheDuty_IsRecordedAsNotCleared()
	{
		Enter();
		_recorder.GameWiped(At(100));
		var saved = _recorder.Snapshot(At(30 + 600));

		var restarted = new RunRecorder();
		restarted.Restore(saved!);
		var run = restarted.Arrived(0, throughDutyFinder: false, At(5000));

		Assert.NotNull(run);
		Assert.Equal(saved!.Id, run.Id);
		Assert.False(run.Cleared);
		Assert.Equal(1, run.Wipes);
		Assert.Equal(600, run.DurationSeconds);
		Assert.Equal(At(20), run.DatePlayed);
		Assert.False(restarted.IsAway);
	}

	private RunRecorder TurnOffAndRestart(List<uint> dropped)
	{
		Enter();
		_recorder.GameWiped(At(100));
		var saved = _recorder.TurnedOff(At(130));
		Assert.True(saved!.PluginTurnedOff);

		var restarted = new RunRecorder();
		restarted.EndMissed += dropped.Add;
		restarted.Restore(saved);
		return restarted;
	}

	[Fact]
	public void ARunTurnedOff_ThatEndedWhileOff_IsDropped_AndSaysSo()
	{
		var dropped = new List<uint>();
		var restarted = TurnOffAndRestart(dropped);

		Assert.Null(restarted.Arrived(0, throughDutyFinder: false, At(2000)));

		Assert.Equal([Vault], dropped);
		Assert.False(restarted.IsAway);
		Assert.Null(restarted.Snapshot(At(2000)));
	}

	[Fact]
	public void ARunTurnedOff_ThenClearedOnceBack_IsLogged_CountingTheTimeOff()
	{
		var dropped = new List<uint>();
		var restarted = TurnOffAndRestart(dropped);

		Assert.Null(restarted.Arrived(Vault, throughDutyFinder: true, At(730)));
		Assert.False(restarted.LoadedIn(At(730)));
		restarted.GameWiped(At(760));
		var run = restarted.Completed(At(830));

		Assert.True(run!.Cleared);
		Assert.Equal(100 + 600 + 100, run.DurationSeconds);
		Assert.Equal(2, run.Wipes);
		Assert.Equal(At(20), run.DatePlayed);
		Assert.Empty(dropped);
	}

	[Fact]
	public void ARunTurnedOff_ThenLeftOnceBack_IsDropped()
	{
		var dropped = new List<uint>();
		var restarted = TurnOffAndRestart(dropped);
		restarted.Arrived(Vault, throughDutyFinder: true, At(730));

		Assert.Null(restarted.Arrived(0, throughDutyFinder: false, At(800)));
		Assert.Equal([Vault], dropped);
	}

	[Fact]
	public void ARunTurnedOff_StaysMarked_AcrossACrashOnceBack()
	{
		var dropped = new List<uint>();
		var restarted = TurnOffAndRestart(dropped);
		restarted.Arrived(Vault, throughDutyFinder: true, At(730));
		var saved = restarted.Snapshot(At(800));
		Assert.True(saved!.PluginTurnedOff);
		Assert.False(saved.ClockRanWhileOff);

		var crashed = new RunRecorder();
		crashed.EndMissed += dropped.Add;
		crashed.Restore(saved);

		Assert.Null(crashed.Arrived(0, throughDutyFinder: false, At(5000)));
		Assert.Equal([Vault], dropped);
	}

	[Fact]
	public void ARunTurnedOffTwice_BeforeComingBack_StillCountsTheTimeOff()
	{
		var restarted = TurnOffAndRestart([]);
		var saved = restarted.TurnedOff(At(300));
		Assert.True(saved!.ClockRanWhileOff);
		Assert.Equal(At(130), saved.LastSeenAt);

		var again = new RunRecorder();
		again.Restore(saved);
		again.Arrived(Vault, throughDutyFinder: true, At(730));

		Assert.Equal(100 + 600 + 100, again.Completed(At(830))!.DurationSeconds);
	}

	[Fact]
	public void ARunTurnedOffWhileLoggedOut_DoesntCountTheTimeOff()
	{
		Enter();
		_recorder.LoggedOut(At(130));
		var saved = _recorder.TurnedOff(At(500));
		Assert.False(saved!.ClockRanWhileOff);

		var restarted = new RunRecorder();
		restarted.Restore(saved);
		restarted.Arrived(Vault, throughDutyFinder: true, At(730));
		Assert.True(restarted.LoadedIn(At(740)));

		Assert.Equal(100 + 100, restarted.Completed(At(840))!.DurationSeconds);
	}

	[Fact]
	public void ARunTurnedOffBeforeTheDutyCommenced_StartsTheClockWhenItsSeen()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		_recorder.LoadedIn(At(25));
		_recorder.JobKnown(Paladin);
		var saved = _recorder.TurnedOff(At(26));
		Assert.False(saved!.ClockRanWhileOff);

		var restarted = new RunRecorder();
		restarted.Restore(saved);
		restarted.Arrived(Vault, throughDutyFinder: true, At(200));
		Assert.True(restarted.Commenced(At(200)));

		Assert.Equal(100, restarted.Completed(At(300))!.DurationSeconds);
	}

	[Fact]
	public void TurningOff_WithNoRun_SavesNothing()
	{
		Assert.Null(_recorder.TurnedOff(At(0)));
	}

	[Fact]
	public void TheSnapshot_CountsTheTimeUpToNow()
	{
		Enter();
		_recorder.GameWiped(At(100));

		var snapshot = _recorder.Snapshot(At(90));

		Assert.Equal(60, snapshot!.ElapsedSeconds);
		Assert.Equal(At(20), snapshot.EnteredAt);
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

	private static Party Four(bool dead, bool inCombat = false)
	{
		return new Party([.. Enumerable.Range(1, 4).Select(id => new PartyMember((uint)id, dead, !dead && inCombat))]);
	}

	private void DungeonWipe(int from)
	{
		_recorder.PartySeen(Four(dead: false, inCombat: true), At(from));
		_recorder.PartySeen(Four(dead: true), At(from + 10));
	}

	[Fact]
	public void ADungeonWipe_IsInTheRun()
	{
		Enter();
		DungeonWipe(100);
		_recorder.PartySeen(Four(dead: false), At(130));

		var run = _recorder.Completed(At(1000));

		Assert.Equal(1, run!.Wipes);
	}

	[Fact]
	public void PartySeen_SaysWhenItCountedAWipe()
	{
		Enter();

		Assert.False(_recorder.PartySeen(Four(dead: false, inCombat: true), At(100)));
		Assert.True(_recorder.PartySeen(Four(dead: true), At(110)));
		Assert.False(_recorder.PartySeen(Four(dead: true), At(111)));
	}

	[Fact]
	public void ATrialWipe_FromThePartyAndTheGame_IsOneWipe()
	{
		Enter();
		DungeonWipe(100);
		_recorder.GameWiped(At(115));

		Assert.Equal(1, _recorder.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void WipesFromThePartyAndTheGame_AddUp_WhenTheyreDifferentWipes()
	{
		Enter();
		DungeonWipe(100);
		_recorder.PartySeen(Four(dead: false), At(130));
		_recorder.GameWiped(At(400));
		DungeonWipe(600);

		Assert.Equal(3, _recorder.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void TheParty_CountsForNothing_BeforeARun()
	{
		Assert.False(_recorder.PartySeen(Four(dead: false, inCombat: true), At(0)));
		Assert.False(_recorder.PartySeen(Four(dead: true), At(5)));

		Enter(popAt: 10, arriveAt: 20);

		Assert.Equal(0, _recorder.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void TheParty_CountsForNothing_WhileAway()
	{
		Enter();
		_recorder.LoggedOut(At(100));
		DungeonWipe(110);
		_recorder.GameWiped(At(125));

		_recorder.Arrived(Vault, throughDutyFinder: true, At(200));
		_recorder.LoadedIn(At(205));

		Assert.Equal(0, _recorder.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void TheParty_CountsForNothing_AfterTheRunEnds()
	{
		Enter();
		var run = _recorder.Completed(At(100));

		DungeonWipe(110);
		_recorder.GameWiped(At(125));

		Assert.Equal(0, run!.Wipes);
		Assert.Null(_recorder.Snapshot(At(130)));
	}

	[Fact]
	public void TheParty_CountsForNothing_AfterARestore_UntilTheyreBack()
	{
		Enter();
		var saved = _recorder.Snapshot(At(100));
		var restarted = new RunRecorder();
		restarted.Restore(saved!);

		restarted.PartySeen(Four(dead: false, inCombat: true), At(110));
		restarted.PartySeen(Four(dead: true), At(120));
		restarted.GameWiped(At(125));

		Assert.Equal(0, restarted.Snapshot(At(130))!.Wipes);
	}

	[Fact]
	public void LoggingOut_ForgetsTheFight()
	{
		Enter();
		_recorder.PartySeen(Four(dead: false, inCombat: true), At(100));
		_recorder.PartySeen(new Party([Dead(1), Dead(2), Dead(3), Fighting(4)]), At(110));
		_recorder.PartySeen(new Party([Idle(1), Idle(2), Idle(3), Fighting(4)]), At(120));
		_recorder.LoggedOut(At(125));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(200));
		_recorder.LoadedIn(At(205));

		_recorder.PartySeen(new Party([Idle(1), Idle(2), Idle(3), Fighting(4)]), At(210));
		_recorder.PartySeen(new Party([Idle(1), Idle(2), Idle(3), Dead(4)]), At(220));

		Assert.Equal(0, _recorder.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void AZoneChangeInsideTheDuty_KeepsTheFight()
	{
		Enter();
		_recorder.PartySeen(Four(dead: false, inCombat: true), At(100));
		_recorder.PartySeen(new Party([Dead(1), Dead(2), Dead(3), Fighting(4)]), At(110));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(115));

		_recorder.PartySeen(Four(dead: true), At(120));

		Assert.Equal(1, _recorder.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void TheWipesSoFar_CarryAcrossARestore()
	{
		Enter();
		DungeonWipe(100);
		var saved = _recorder.Snapshot(At(200));
		Assert.Equal(1, saved!.Wipes);

		var restarted = new RunRecorder();
		restarted.Restore(saved);
		restarted.Arrived(Vault, throughDutyFinder: true, At(300));
		restarted.LoadedIn(At(305));
		restarted.PartySeen(Four(dead: false, inCombat: true), At(400));
		restarted.PartySeen(Four(dead: true), At(410));

		var run = restarted.Completed(At(1000));
		Assert.Equal(2, run!.Wipes);
	}

	[Fact]
	public void ARestoreDuringAWipe_DoesntCountItAgain()
	{
		Enter();
		DungeonWipe(100);
		var saved = _recorder.Snapshot(At(112));

		var restarted = new RunRecorder();
		restarted.Restore(saved!);
		restarted.Arrived(Vault, throughDutyFinder: true, At(150));
		restarted.LoadedIn(At(155));
		restarted.PartySeen(Four(dead: true), At(156));
		restarted.PartySeen(Four(dead: true), At(160));
		restarted.PartySeen(Four(dead: false), At(170));

		Assert.Equal(1, restarted.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void ARestoreMidFight_StillCountsTheWipeItSees()
	{
		Enter();
		var saved = _recorder.Snapshot(At(100));

		var restarted = new RunRecorder();
		restarted.Restore(saved!);
		restarted.Arrived(Vault, throughDutyFinder: true, At(150));
		restarted.LoadedIn(At(155));
		restarted.PartySeen(new Party([Dead(1), Dead(2), Fighting(3), Fighting(4)]), At(156));
		restarted.PartySeen(Four(dead: true), At(170));

		Assert.Equal(1, restarted.Completed(At(1000))!.Wipes);
	}

	[Fact]
	public void ANewRun_StartsWithANewParty()
	{
		Enter();
		_recorder.PartySeen(Four(dead: false, inCombat: true), At(100));
		_recorder.PartySeen(new Party([Dead(1), Dead(2), Dead(3), Fighting(4)]), At(110));
		_recorder.Completed(At(120));
		_recorder.Arrived(0, throughDutyFinder: false, At(130));

		Enter(popAt: 200, arriveAt: 230);
		_recorder.PartySeen(new Party([Idle(1), Idle(2), Idle(3), Fighting(4)]), At(300));
		_recorder.PartySeen(new Party([Idle(1), Idle(2), Idle(3), Dead(4)]), At(310));

		Assert.Equal(0, _recorder.Completed(At(1000))!.Wipes);
	}

	private static PartyMember Fighting(uint id) => new(id, Dead: false, InCombat: true);

	private static PartyMember Idle(uint id) => new(id, Dead: false, InCombat: false);

	private static PartyMember Dead(uint id) => new(id, Dead: true, InCombat: false);

	private void TankDies(int from, RunRecorder? recorder = null)
	{
		recorder ??= _recorder;
		recorder.PartySeen(Four(dead: false, inCombat: true), At(from));
		recorder.PartySeen(new Party([Dead(1), Fighting(2), Fighting(3), Fighting(4)]), At(from + 5));
		recorder.PartySeen(Four(dead: false, inCombat: true), At(from + 10));
	}

	[Fact]
	public void TheDeaths_AreInTheRun()
	{
		Enter();
		TankDies(100);
		TankDies(200);
		DungeonWipe(300);

		Assert.Equal(6, _recorder.Completed(At(1000))!.Deaths);
	}

	[Fact]
	public void ARunNobodyDiedIn_HasNoDeaths_NotNone()
	{
		Enter();

		Assert.Equal(0, _recorder.Completed(At(1000))!.Deaths);
	}

	[Fact]
	public void PartySeen_SaysWhenItCountedADeath_SoTheRunsSaved()
	{
		Enter();
		_recorder.PartySeen(Four(dead: false, inCombat: true), At(100));

		Assert.True(_recorder.PartySeen(new Party([Dead(1), Fighting(2), Fighting(3), Fighting(4)]), At(105)));
		Assert.False(_recorder.PartySeen(new Party([Dead(1), Fighting(2), Fighting(3), Fighting(4)]), At(106)));
		Assert.Equal(1, _recorder.Snapshot(At(107))!.Deaths);
	}

	[Fact]
	public void Deaths_CountForNothing_BeforeARun()
	{
		TankDies(0);
		Enter(popAt: 20, arriveAt: 30);

		Assert.Equal(0, _recorder.Completed(At(1000))!.Deaths);
	}

	[Fact]
	public void Deaths_CountForNothing_WhileAway()
	{
		Enter();
		_recorder.LoggedOut(At(100));
		TankDies(110);

		_recorder.Arrived(Vault, throughDutyFinder: true, At(200));
		_recorder.LoadedIn(At(205));

		Assert.Equal(0, _recorder.Completed(At(1000))!.Deaths);
	}

	[Fact]
	public void Deaths_CountForNothing_AfterTheRunEnds()
	{
		Enter();
		var run = _recorder.Completed(At(100));

		TankDies(110);
		_recorder.Arrived(0, throughDutyFinder: false, At(130));
		Enter(popAt: 200, arriveAt: 230);

		Assert.Equal(0, run!.Deaths);
		Assert.Equal(0, _recorder.Completed(At(1000))!.Deaths);
	}

	[Fact]
	public void Deaths_CountForNothing_AfterARestore_UntilTheyreBack()
	{
		Enter();
		var saved = _recorder.Snapshot(At(100));
		var restarted = new RunRecorder();
		restarted.Restore(saved!);

		TankDies(110, restarted);

		Assert.Equal(0, restarted.Snapshot(At(130))!.Deaths);
	}

	[Fact]
	public void TheDeathsSoFar_SurviveADisconnectAndReturn_WithoutCountingTheDeadAgain()
	{
		Enter();
		TankDies(100);
		_recorder.PartySeen(new Party([Fighting(1), Dead(2), Fighting(3), Fighting(4)]), At(150));
		_recorder.LoggedOut(At(160));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(200));
		_recorder.LoadedIn(At(205));

		_recorder.PartySeen(new Party([Fighting(1), Dead(2), Dead(3), Fighting(4)]), At(210));
		_recorder.PartySeen(new Party([Dead(1), Dead(2), Dead(3), Fighting(4)]), At(220));

		Assert.Equal(3, _recorder.Completed(At(1000))!.Deaths);
	}

	[Fact]
	public void TheDeathsSoFar_SurviveACrashAndRestart_WithoutCountingTheDeadAgain()
	{
		Enter();
		TankDies(100);
		_recorder.PartySeen(new Party([Fighting(1), Dead(2), Fighting(3), Fighting(4)]), At(150));
		var saved = _recorder.Snapshot(At(160));
		Assert.Equal(2, saved!.Deaths);

		var restarted = new RunRecorder();
		restarted.Restore(saved);
		restarted.Arrived(Vault, throughDutyFinder: true, At(300));
		restarted.LoadedIn(At(305));
		restarted.PartySeen(new Party([Fighting(1), Dead(2), Fighting(3), Fighting(4)]), At(310));
		restarted.PartySeen(new Party([Fighting(1), Dead(2), Dead(3), Fighting(4)]), At(320));

		Assert.Equal(3, restarted.Completed(At(1000))!.Deaths);
	}

	[Fact]
	public void TheDeathsSoFar_SurviveThePluginBeingTurnedOff()
	{
		Enter();
		TankDies(100);
		var saved = _recorder.TurnedOff(At(130));

		var restarted = new RunRecorder();
		restarted.Restore(saved!);
		restarted.Arrived(Vault, throughDutyFinder: true, At(300));
		restarted.LoadedIn(At(305));
		TankDies(400, restarted);

		Assert.Equal(2, restarted.Completed(At(1000))!.Deaths);
	}

	[Fact]
	public void ARunSavedWithoutDeaths_CarriesOnWithout_RatherThanSendPartOfThem()
	{
		Enter();
		var saved = _recorder.Snapshot(At(100))! with { Deaths = null };

		var restarted = new RunRecorder();
		var notes = Notes(restarted);
		restarted.Restore(saved);
		restarted.Arrived(Vault, throughDutyFinder: true, At(300));
		restarted.LoadedIn(At(305));
		Assert.False(restarted.PartySeen(Four(dead: false, inCombat: true), At(400)));
		Assert.False(restarted.PartySeen(new Party([Dead(1), Fighting(2), Fighting(3), Fighting(4)]), At(405)));

		Assert.Null(restarted.Snapshot(At(410))!.Deaths);
		Assert.Null(restarted.Completed(At(1000))!.Deaths);
		Assert.DoesNotContain(notes, note => note is RunNote.DeathsCounted);
	}

	private (Run Run, List<RunNote> Notes) RunWithDeaths(int deaths)
	{
		Enter();
		var saved = _recorder.Snapshot(At(100))! with { Deaths = deaths };
		var restarted = new RunRecorder();
		var notes = Notes(restarted);
		restarted.Restore(saved);
		restarted.Arrived(Vault, throughDutyFinder: true, At(300));
		restarted.LoadedIn(At(305));
		return (restarted.Completed(At(1000))!, notes);
	}

	[Fact]
	public void TheMostDeathsTheSiteTakes_AreSent()
	{
		Assert.Equal(999, RunWithDeaths(999).Run.Deaths);
	}

	[Fact]
	public void MoreDeathsThanTheSiteTakes_AreLeftOut_AndSaySo()
	{
		var (run, notes) = RunWithDeaths(1000);

		Assert.Null(run.Deaths);
		Assert.Contains(new RunNote.TooManyDeaths(1000), notes);
	}

	[Fact]
	public void Deaths_AreNotedWithTheRunsCount_OnceAFrame()
	{
		Enter();
		TankDies(100);
		var notes = Notes(_recorder);

		DungeonWipe(200);

		Assert.Equal([new RunNote.DeathsCounted(5)], notes.OfType<RunNote.DeathsCounted>());
	}

	private List<RunNote> Notes(RunRecorder recorder)
	{
		var notes = new List<RunNote>();
		recorder.Noted += notes.Add;
		return notes;
	}

	[Fact]
	public void ARun_IsNotedFromItsPopToItsEnd()
	{
		var notes = Notes(_recorder);
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		_recorder.LoadedIn(At(25));
		_recorder.JobKnown(Paladin);
		_recorder.Commenced(At(30));
		DungeonWipe(100);
		var run = _recorder.Completed(At(1030));

		Assert.Equal(
			[
				new RunNote.Popped(Replacement: false),
				new RunNote.Started(Vault, Replacement: false),
				new RunNote.JobKnown(Paladin),
				new RunNote.ClockStarted(ClockStartReason.DutyCommenced),
				new RunNote.DeathsCounted(4),
				new RunNote.Wiped(1, WipeSource.Party, 4),
				new RunNote.Ended(run!, WhileAway: false),
			],
			notes.Where(note => note is not (RunNote.PartyChanged or RunNote.FightStarted or RunNote.FightOver or RunNote.MemberDied)));
	}

	[Fact]
	public void AReplacementsClock_IsNotedAsStartingOnLoadingIn()
	{
		var notes = Notes(_recorder);
		_recorder.Popped(isMentorRoulette: true, inProgress: true, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		_recorder.LoadedIn(At(25));

		Assert.Equal(
			[new RunNote.Popped(Replacement: true), new RunNote.Started(Vault, Replacement: true), new RunNote.ClockStarted(ClockStartReason.LoadedIn)],
			notes);
	}

	[Fact]
	public void TheClock_IsNotedAsStartingFromTheDutyHavingStarted_WhenTheMessageWasMissed()
	{
		var notes = Notes(_recorder);
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		_recorder.Commenced(At(40), dutyHadStarted: true);

		Assert.Equal(new RunNote.ClockStarted(ClockStartReason.DutyHadStarted), notes.Last());
	}

	[Fact]
	public void LoggingOutAndComingBack_IsNoted()
	{
		Enter();
		var notes = Notes(_recorder);
		_recorder.LoggedOut(At(130));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(500));
		_recorder.LoadedIn(At(505));

		Assert.Equal(
			[new RunNote.LoggedOut(100), new RunNote.BackInRun(Vault), new RunNote.ClockStarted(ClockStartReason.LoadedIn)],
			notes);
	}

	[Fact]
	public void AZoneChangeInsideTheDuty_IsntNotedAsComingBack()
	{
		Enter();
		var notes = Notes(_recorder);

		_recorder.Arrived(Vault, throughDutyFinder: true, At(300));

		Assert.Empty(notes);
	}

	[Fact]
	public void LeavingEarly_IsNotedAsLeaving()
	{
		Enter();
		var notes = Notes(_recorder);

		var run = _recorder.Arrived(0, throughDutyFinder: false, At(300));

		Assert.Equal([new RunNote.Ended(run!, WhileAway: false)], notes);
	}

	[Fact]
	public void ARunThatEndedWhileAway_IsNotedAsNotComingBack()
	{
		Enter();
		var saved = _recorder.Snapshot(At(300));
		var restarted = new RunRecorder();
		var notes = Notes(restarted);
		restarted.Restore(saved!);

		var run = restarted.Arrived(0, throughDutyFinder: false, At(5000));

		Assert.Equal([new RunNote.Restored(Vault, PluginWasOff: false), new RunNote.Ended(run!, WhileAway: true)], notes);
	}

	[Fact]
	public void ARunNeverLoadedInto_IsNotedAsDropped()
	{
		_recorder.Popped(isMentorRoulette: true, inProgress: false, At(0));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(20));
		var notes = Notes(_recorder);

		_recorder.Arrived(0, throughDutyFinder: false, At(30));

		Assert.Equal([new RunNote.Dropped(Vault, DropReason.NeverLoadedIn)], notes);
	}

	[Fact]
	public void ARunThePluginWasOffFor_IsNotedAsDropped()
	{
		Enter();
		var saved = _recorder.TurnedOff(At(130));
		var restarted = new RunRecorder();
		var notes = Notes(restarted);
		restarted.Restore(saved!);

		restarted.Arrived(0, throughDutyFinder: false, At(2000));

		Assert.Equal([new RunNote.Restored(Vault, PluginWasOff: true), new RunNote.Dropped(Vault, DropReason.PluginWasOff)], notes);
	}

	[Fact]
	public void EventsWithNoRun_AreNotedAsIgnored()
	{
		var notes = Notes(_recorder);
		_recorder.Popped(isMentorRoulette: false, inProgress: false, At(0));
		_recorder.GameWiped(At(10));

		Assert.Equal([new RunNote.PopIgnored(), new RunNote.WipeIgnored()], notes);
		Assert.All(notes, note => Assert.IsAssignableFrom<RunNote.Ignored>(note));
	}

	[Fact]
	public void TheGamesWipeMessage_IsNotedWithTheRunsCount()
	{
		Enter();
		_recorder.GameWiped(At(100));
		var notes = Notes(_recorder);

		_recorder.GameWiped(At(400));

		Assert.Equal([new RunNote.Wiped(2, WipeSource.GameMessage, 0)], notes);
	}

	[Fact]
	public void NothingDuringARun_IsIgnored()
	{
		var notes = Notes(_recorder);
		Enter();
		DungeonWipe(100);
		_recorder.GameWiped(At(115));
		_recorder.LoggedOut(At(200));
		_recorder.Arrived(Vault, throughDutyFinder: true, At(300));
		_recorder.LoadedIn(At(305));
		_recorder.Completed(At(400));

		Assert.All(notes, note => Assert.False(note is RunNote.Ignored));
	}
}
