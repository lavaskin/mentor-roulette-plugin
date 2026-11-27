using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Recording;

namespace MentorRouletteTracker.Core.Tests;

/// <summary>
/// Each test is a timeline: one <see cref="See"/> per frame that changes something, at its second, with the party as
/// that frame shows it. Members not showing in the game read as out of combat, so <see cref="Idle"/> covers them too.
/// </summary>
public class PartyWatcherTests
{
	private const uint Tank = 1;
	private const uint Healer = 2;
	private const uint Dps = 3;
	private const uint Dps2 = 4;
	private const uint Newcomer = 5;

	private static readonly DateTime Start = new(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc);

	private readonly PartyWatcher _watcher = new();
	private readonly List<WipeSource> _wipes = [];
	private readonly List<RunNote> _notes = [];

	public PartyWatcherTests()
	{
		_watcher.Noted += _notes.Add;
	}

	private static DateTime At(double seconds) => Start.AddSeconds(seconds);

	private static PartyMember Fighting(uint id) => new(id, Dead: false, InCombat: true);

	private static PartyMember Idle(uint id) => new(id, Dead: false, InCombat: false);

	private static PartyMember Dead(uint id) => new(id, Dead: true, InCombat: false);

	private static PartyMember Other(PartyMember member) => member with { OtherParty = true };

	/// <returns>Whether this frame counted a wipe</returns>
	private bool See(double second, params PartyMember[] members)
	{
		if (_watcher.Seen(new Party(members), At(second)) is var (source, _))
		{
			_wipes.Add(source);
			return true;
		}

		return false;
	}

	/// <returns>Whether the message counted a wipe</returns>
	private bool GameMessage(double second)
	{
		if (_watcher.GameWiped(At(second)) is { } source)
		{
			_wipes.Add(source);
			return true;
		}

		return false;
	}

	private void AllFighting(double second)
	{
		See(second, Fighting(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
	}

	/// <summary>
	/// The tank, the healer and one DPS die one at a time, and the other DPS fights on
	/// </summary>
	private void ThreeDie(double from)
	{
		See(from, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(from + 5, Dead(Tank), Dead(Healer), Fighting(Dps), Fighting(Dps2));
		See(from + 10, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));
	}

	// What a wipe is

	[Fact]
	public void EveryoneDyingAtOnce_IsOneWipe()
	{
		AllFighting(0);

		Assert.True(See(30, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
		Assert.Equal([WipeSource.Party], _wipes);
	}

	[Fact]
	public void DyingOneByOne_IsOneWipe_CountedAsTheLastDies()
	{
		AllFighting(0);
		ThreeDie(10);
		Assert.Empty(_wipes);

		Assert.True(See(25, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
		Assert.Single(_wipes);
	}

	[Fact]
	public void AWipe_IsCountedOnce_WhileEveryoneLiesDead()
	{
		AllFighting(0);
		See(10, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));

		for (var second = 11; second < 60; second++)
		{
			Assert.False(See(second, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
		}

		Assert.Single(_wipes);
	}

	[Fact]
	public void OneReturnsToTheStart_WhileTheRestFightOnAndDie_IsOneWipe()
	{
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));

		// The tank returns to the start: alive, and out of combat outside the boss's wall
		See(20, Idle(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(30, Idle(Tank), Dead(Healer), Fighting(Dps), Fighting(Dps2));
		See(40, Idle(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));
		Assert.Empty(_wipes);

		Assert.True(See(50, Idle(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
		Assert.Single(_wipes);
	}

	[Fact]
	public void EachReturningToTheStartAfterDying_IsOneWipe_AtTheLastDeath()
	{
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(15, Idle(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(20, Idle(Tank), Dead(Healer), Fighting(Dps), Fighting(Dps2));
		See(25, Idle(Tank), Idle(Healer), Fighting(Dps), Fighting(Dps2));
		See(30, Idle(Tank), Idle(Healer), Dead(Dps), Fighting(Dps2));
		See(35, Idle(Tank), Idle(Healer), Idle(Dps), Fighting(Dps2));
		Assert.Empty(_wipes);

		Assert.True(See(40, Idle(Tank), Idle(Healer), Idle(Dps), Dead(Dps2)));
		Assert.Single(_wipes);
	}

	[Fact]
	public void ThreeDying_AndTheFourthFinishingTheFight_IsNoWipe()
	{
		AllFighting(0);
		ThreeDie(10);

		See(60, Dead(Tank), Dead(Healer), Dead(Dps), Idle(Dps2));

		Assert.Empty(_wipes);
	}

	[Fact]
	public void DeathsInAFightTheyWon_DontCountTowardsTheNext_OnceTheyreUp()
	{
		AllFighting(0);
		ThreeDie(10);
		See(60, Dead(Tank), Dead(Healer), Dead(Dps), Idle(Dps2));
		See(70, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));

		AllFighting(100);
		Assert.False(See(110, Fighting(Tank), Fighting(Healer), Fighting(Dps), Dead(Dps2)));
		Assert.False(See(120, Idle(Tank), Idle(Healer), Idle(Dps), Dead(Dps2)));

		Assert.Empty(_wipes);
	}

	[Fact]
	public void MembersStillDead_WhenTheNextFightStarts_CountAsHavingDiedInIt()
	{
		// Nobody raised the three, and the last one pulled the next pack alone
		AllFighting(0);
		ThreeDie(10);
		See(60, Dead(Tank), Dead(Healer), Dead(Dps), Idle(Dps2));

		See(90, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));
		Assert.Empty(_wipes);

		Assert.True(See(100, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
		Assert.Single(_wipes);
	}

	[Fact]
	public void AMemberWhoDiedAndIsUpBetweenFights_StartsTheNextFightAfresh()
	{
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(20, Dead(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));

		// Raised between fights, then idle while the others fight and die
		See(30, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));
		See(40, Idle(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(50, Idle(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));

		Assert.Empty(_wipes);
	}

	[Fact]
	public void EveryoneDyingOnce_ButRaisedAndFightingOn_IsNoWipe()
	{
		// A messy fight the party wins: each dies once and is raised back into the fight
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(15, Fighting(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(20, Fighting(Tank), Dead(Healer), Fighting(Dps), Fighting(Dps2));
		See(25, Fighting(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(30, Fighting(Tank), Fighting(Healer), Dead(Dps), Fighting(Dps2));
		See(35, Fighting(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(40, Fighting(Tank), Fighting(Healer), Fighting(Dps), Dead(Dps2));
		See(45, Fighting(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(90, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));

		Assert.Empty(_wipes);
	}

	[Fact]
	public void EveryoneWhoDiedReturningToTheStart_WhileTheLastStandsIdle_IsNoWipe()
	{
		// The boss reset with one member alive: not everyone died
		AllFighting(0);
		See(10, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));
		See(20, Idle(Tank), Idle(Healer), Idle(Dps), Fighting(Dps2));
		See(30, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));

		Assert.Empty(_wipes);
	}

	[Fact]
	public void TheLastMemberStandingLeaving_IsNoWipe()
	{
		// Nobody died as they left, and the dead return to the start as usual
		AllFighting(0);
		See(10, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));

		Assert.False(See(20, Dead(Tank), Dead(Healer), Dead(Dps)));
	}

	[Fact]
	public void AMemberRaisedIntoTheFight_DyingAgainWithTheRest_IsOneWipe()
	{
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(15, Fighting(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(20, Fighting(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		Assert.Empty(_wipes);

		Assert.True(See(25, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
		Assert.Single(_wipes);
	}

	[Fact]
	public void DyingTwice_DoesntStandInForAnotherMembersDeath()
	{
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(15, Fighting(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(20, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(25, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));
		See(60, Dead(Tank), Dead(Healer), Dead(Dps), Idle(Dps2));

		Assert.Empty(_wipes);
	}

	[Fact]
	public void AFightWithNoDeaths_CountsNothing()
	{
		for (var pull = 0; pull < 5; pull++)
		{
			AllFighting(pull * 100);
			See(pull * 100 + 60, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));
		}

		Assert.Empty(_wipes);
	}

	[Fact]
	public void EveryoneKilledInOneFrame_BeforeTheirCombatShowed_IsAWipe()
	{
		See(0, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));

		Assert.True(See(1, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
	}

	[Fact]
	public void TwoWipesOnTheSameBoss_AreTwo()
	{
		AllFighting(0);
		See(30, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		See(60, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));

		AllFighting(120);
		See(150, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));

		Assert.Equal(2, _wipes.Count);
	}

	[Fact]
	public void ReturningOneByOneAfterAWipe_AndDyingAgain_IsASecondWipe()
	{
		AllFighting(0);
		See(30, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		See(40, Idle(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		See(45, Idle(Tank), Idle(Healer), Dead(Dps), Dead(Dps2));
		See(50, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));
		Assert.Single(_wipes);

		AllFighting(120);
		See(150, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));

		Assert.Equal(2, _wipes.Count);
	}

	// Who's in the party

	[Fact]
	public void OnYourOwn_YourDeathIsAWipe()
	{
		See(0, Fighting(Tank));

		Assert.True(See(10, Dead(Tank)));
	}

	[Fact]
	public void NoOneInTheParty_CountsNothing()
	{
		See(0);
		See(1);

		Assert.Empty(_wipes);
	}

	[Fact]
	public void AMemberWhoLeaves_NoLongerHasToDie()
	{
		AllFighting(0);
		See(10, Fighting(Tank), Fighting(Healer), Fighting(Dps));
		See(20, Dead(Tank), Dead(Healer), Fighting(Dps));

		Assert.True(See(30, Dead(Tank), Dead(Healer), Dead(Dps)));
	}

	[Fact]
	public void AReplacementInTheSameSlot_IsSomeoneNew_WhoHasToDieToo()
	{
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));

		// The tank who died leaves, and a replacement joins in their slot
		See(20, Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(30, Fighting(Newcomer), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		Assert.False(See(40, Fighting(Newcomer), Dead(Healer), Dead(Dps), Dead(Dps2)));

		Assert.True(See(50, Dead(Newcomer), Dead(Healer), Dead(Dps), Dead(Dps2)));
		Assert.Single(_wipes);
	}

	[Fact]
	public void AMemberJoiningMidFight_HasToDieToo()
	{
		See(0, Fighting(Tank), Fighting(Healer), Fighting(Dps));
		See(10, Fighting(Tank), Fighting(Healer), Fighting(Dps), Fighting(Newcomer));

		Assert.False(See(20, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Newcomer)));
		Assert.True(See(30, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Newcomer)));
	}

	[Fact]
	public void MembersAreToldApartById_NotTheirOrder()
	{
		AllFighting(0);
		See(10, Fighting(Dps2), Dead(Tank), Fighting(Dps), Fighting(Healer));
		See(20, Dead(Healer), Fighting(Dps), Dead(Tank), Fighting(Dps2));

		Assert.Empty(_wipes);
		Assert.True(See(30, Dead(Dps2), Dead(Dps), Dead(Healer), Dead(Tank)));
	}

	[Fact]
	public void TheSameMemberTwiceInAFrame_CountsOnce()
	{
		See(0, Fighting(Tank), Fighting(Healer), Fighting(Healer));
		See(10, Dead(Tank), Fighting(Healer), Dead(Healer));

		Assert.Empty(_wipes);
	}

	// Already dead when first seen

	[Fact]
	public void FirstSeenDead_CountsTowardsTheFight()
	{
		// The plugin started with the tank and the healer dead, and the DPS fighting on
		See(0, Dead(Tank), Dead(Healer), Fighting(Dps), Fighting(Dps2));
		See(10, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));

		Assert.True(See(20, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
	}

	[Fact]
	public void FirstSeenDeadMidFight_ThenBackAtTheStart_StillCountsAsHavingDiedThisFight()
	{
		// The tank's slot loads in mid-fight, dead, then they return to the start
		See(0, Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(5, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(10, Idle(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(20, Idle(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));

		Assert.True(See(30, Idle(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
	}

	[Fact]
	public void EveryoneFirstSeenDead_IsNoWipe_SinceItWasAlreadyCounted()
	{
		See(0, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		See(10, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		See(20, Idle(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		See(30, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));

		Assert.Empty(_wipes);
	}

	[Fact]
	public void FirstSeenAfterAWipe_WithSomeAlreadyBackAtTheStart_IsNoWipe()
	{
		See(0, Idle(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		See(10, Idle(Tank), Idle(Healer), Dead(Dps), Dead(Dps2));

		Assert.Empty(_wipes);
	}

	[Fact]
	public void AMemberBackAfterBeingAway_IsSeenAfresh()
	{
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));

		// The tank disconnects and comes back alive, after the fight they died in
		See(20, Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(30, Idle(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(40, Idle(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));

		Assert.Empty(_wipes);
	}

	// Combat that can't be seen

	[Fact]
	public void SomeoneNotShowing_CountsAsOutOfCombat()
	{
		// Only the tank shows in combat: once they're dead, the fight looks over to the watcher
		See(0, Fighting(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));
		See(10, Dead(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));

		Assert.Contains(new RunNote.FightOver(FightEnd.NobodyInCombat), _notes);
		Assert.Empty(_wipes);
	}

	[Fact]
	public void TheKnownMiss_BackAtTheStart_WithTheFightTooFarAwayToShow()
	{
		// The player died, returned to the start, and can't see the others fight: the fight looks over, and its
		// deaths are forgotten, so the others dying later isn't a wipe. It can be fixed on the site.
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(20, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));
		See(30, Idle(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));

		Assert.Empty(_wipes);
	}

	// The game's wipe message

	[Fact]
	public void TheGamesMessage_OnItsOwn_IsAWipe()
	{
		// The duty failed some other way, like an escort dying
		AllFighting(0);

		Assert.True(GameMessage(30));
		Assert.Equal([WipeSource.GameMessage], _wipes);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(5)]
	[InlineData(30)]
	public void APartyWipe_ThenTheGamesMessage_CountOnce(double after)
	{
		AllFighting(0);
		See(20, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));

		Assert.False(GameMessage(20 + after));
		Assert.Equal([WipeSource.Party], _wipes);
		Assert.Contains(new RunNote.WipeSkipped(WipeSource.GameMessage, TimeSpan.FromSeconds(after)), _notes);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(5)]
	[InlineData(30)]
	public void TheGamesMessage_ThenAPartyWipe_CountOnce(double after)
	{
		// The last death is read after the message: the fade-out came first
		AllFighting(0);
		See(10, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));
		GameMessage(20);

		Assert.False(See(20 + after, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
		Assert.Equal([WipeSource.GameMessage], _wipes);
		Assert.Contains(new RunNote.WipeSkipped(WipeSource.Party, TimeSpan.FromSeconds(after)), _notes);
	}

	[Fact]
	public void APartyWipe_AndTheGamesMessage_31SecondsApart_AreTwo()
	{
		AllFighting(0);
		See(20, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));

		Assert.True(GameMessage(51));
		Assert.Equal([WipeSource.Party, WipeSource.GameMessage], _wipes);
	}

	[Fact]
	public void TheGamesMessage_ThenAPartyWipe_31SecondsApart_AreTwo()
	{
		AllFighting(0);
		See(10, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));
		GameMessage(20);

		Assert.True(See(51, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
		Assert.Equal([WipeSource.GameMessage, WipeSource.Party], _wipes);
	}

	[Fact]
	public void EachWipe_AbsorbsOnlyOneFromTheOtherSide()
	{
		AllFighting(0);
		See(20, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		GameMessage(25);

		Assert.True(GameMessage(28));
		Assert.Equal([WipeSource.Party, WipeSource.GameMessage], _wipes);
	}

	[Fact]
	public void ATrialWipe_WithEveryoneRaisedByTheGame_IsOneWipe()
	{
		AllFighting(0);
		ThreeDie(10);
		See(25, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		See(28, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		GameMessage(30);

		// The game raises everyone at the start, out of combat
		See(32, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));
		See(35, Idle(Tank), Idle(Healer), Idle(Dps), Idle(Dps2));

		Assert.Equal([WipeSource.Party], _wipes);
	}

	[Fact]
	public void TheGamesMessage_EndsTheFight_AndItsDeaths()
	{
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		GameMessage(20);

		// Raised by the game, the tank waits while the others start straight away and die
		See(21, Idle(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(30, Idle(Tank), Dead(Healer), Dead(Dps), Dead(Dps2));

		Assert.Equal([WipeSource.GameMessage], _wipes);
		Assert.Contains(new RunNote.FightOver(FightEnd.GameWipeMessage), _notes);
	}

	// Alliance raids

	private static PartyMember[] Alliance(Func<uint, PartyMember> own, Func<uint, PartyMember> others)
	{
		return
		[
			.. Enumerable.Range(1, 8).Select(id => own((uint)id)),
			.. Enumerable.Range(9, 16).Select(id => Other(others((uint)id))),
		];
	}

	[Fact]
	public void InAnAllianceRaid_YourPartyOf8Dying_IsNoWipe()
	{
		See(0, Alliance(Fighting, Fighting));

		Assert.False(See(10, Alliance(Dead, Fighting)));
		See(60, Alliance(Dead, Idle));
		Assert.Empty(_wipes);
	}

	[Fact]
	public void InAnAllianceRaid_All24Dying_IsAWipe()
	{
		See(0, Alliance(Fighting, Fighting));
		See(10, Alliance(Dead, Fighting));

		Assert.True(See(20, Alliance(Dead, Dead)));
		Assert.Single(_wipes);
	}

	[Fact]
	public void InAnAllianceRaid_TheOtherPartiesFighting_KeepTheFightGoing()
	{
		See(0, Alliance(Fighting, Fighting));
		See(10, Alliance(Dead, Fighting));

		Assert.DoesNotContain(_notes, note => note is RunNote.FightOver);
	}

	// Forgetting

	[Fact]
	public void Reset_ForgetsTheFightsDeaths()
	{
		AllFighting(0);
		See(10, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));
		See(15, Idle(Tank), Idle(Healer), Idle(Dps), Fighting(Dps2));

		_watcher.Reset();
		See(20, Idle(Tank), Idle(Healer), Idle(Dps), Fighting(Dps2));

		Assert.False(See(30, Idle(Tank), Idle(Healer), Idle(Dps), Dead(Dps2)));
	}

	[Fact]
	public void Reset_ThenEveryoneSeenDead_IsNoWipe()
	{
		AllFighting(0);
		See(10, Dead(Tank), Dead(Healer), Dead(Dps), Fighting(Dps2));

		_watcher.Reset();

		Assert.False(See(30, Dead(Tank), Dead(Healer), Dead(Dps), Dead(Dps2)));
	}

	// What it notes for the log

	[Fact]
	public void TheParty_IsNoted_WhenItChanges()
	{
		AllFighting(0);
		AllFighting(1);
		See(2, Fighting(Tank), Fighting(Healer), Fighting(Dps));
		_watcher.Seen(new Party([Fighting(Tank), Fighting(Healer), Fighting(Dps)], NotHere: 1), At(3));

		Assert.Equal(
			[new RunNote.PartyChanged(4, 0, 0), new RunNote.PartyChanged(3, 0, 0), new RunNote.PartyChanged(3, 0, 1)],
			_notes.OfType<RunNote.PartyChanged>());
	}

	[Fact]
	public void AnAlliance_IsNotedWithItsSize()
	{
		See(0, Alliance(Idle, Idle));

		Assert.Equal(new RunNote.PartyChanged(8, 24, 0), _notes.OfType<RunNote.PartyChanged>().Single());
	}

	[Fact]
	public void Fights_AreNotedAsTheyStartAndEnd()
	{
		See(0, Idle(Tank), Idle(Healer));
		See(1, Fighting(Tank), Idle(Healer));
		See(2, Fighting(Tank), Fighting(Healer));
		See(3, Idle(Tank), Idle(Healer));

		Assert.Equal(
			[new RunNote.FightStarted(), new RunNote.FightOver(FightEnd.NobodyInCombat)],
			_notes.Where(note => note is RunNote.FightStarted or RunNote.FightOver));
	}

	[Fact]
	public void DeathsAndMembersUpAgain_AreNotedWithTheirCounts()
	{
		AllFighting(0);
		See(10, Dead(Tank), Fighting(Healer), Fighting(Dps), Fighting(Dps2));
		See(15, Fighting(Tank), Dead(Healer), Fighting(Dps), Fighting(Dps2));
		See(20, Fighting(Tank), Idle(Healer), Fighting(Dps), Fighting(Dps2));

		Assert.Equal(
			[
				new RunNote.MemberDied(1, 1, 4),
				new RunNote.MemberDied(1, 2, 4),
				new RunNote.MemberUp(1, 4),
				new RunNote.MemberUp(0, 4),
			],
			_notes.Where(note => note is RunNote.MemberDied or RunNote.MemberUp));
	}

	[Fact]
	public void TwoDeathsInOneFrame_AreTwoNotes()
	{
		AllFighting(0);
		See(10, Dead(Tank), Dead(Healer), Fighting(Dps), Fighting(Dps2));

		Assert.Equal([new RunNote.MemberDied(2, 2, 4), new RunNote.MemberDied(2, 2, 4)], _notes.OfType<RunNote.MemberDied>());
	}

	[Fact]
	public void ADeathOutsideAFight_IsNotedWithoutTheFightsCount()
	{
		See(0, Idle(Tank), Idle(Healer));
		See(10, Dead(Tank), Idle(Healer));

		Assert.Equal(new RunNote.MemberDied(1, null, 2), _notes.OfType<RunNote.MemberDied>().Single());
	}

	[Fact]
	public void OnlyYourOwnPartysDeaths_AreNoted_InAnAlliance()
	{
		See(0, Alliance(Fighting, Fighting));
		See(10, Alliance(Fighting, Dead));
		Assert.DoesNotContain(_notes, note => note is RunNote.MemberDied);

		See(20, Alliance(Dead, Dead));
		Assert.Equal(8, _notes.OfType<RunNote.MemberDied>().Count());
		Assert.All(_notes.OfType<RunNote.MemberDied>(), died => Assert.Equal(new RunNote.MemberDied(8, 8, 8), died));
	}

	[Fact]
	public void FirstSeenDead_IsntNotedAsADeath()
	{
		See(0, Dead(Tank), Fighting(Healer));

		Assert.DoesNotContain(_notes, note => note is RunNote.MemberDied);
	}

	// Rules that hold whatever happens

	/// <summary>
	/// Thousands of random frames of a party of four, with deaths, raises, returns to the start, combat coming and
	/// going, and members leaving and joining, checked against the rules that hold whatever the order
	/// </summary>
	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(6)]
	[InlineData(7)]
	[InlineData(8)]
	public void RandomRuns_KeepTheRules(int seed)
	{
		var random = new Random(seed);
		var party = new Dictionary<uint, PartyMember>
		{
			[Tank] = Idle(Tank),
			[Healer] = Idle(Healer),
			[Dps] = Idle(Dps),
			[Dps2] = Idle(Dps2),
		};
		var nextId = Newcomer;
		var everDead = new HashSet<uint>();
		var deathSeenSinceWipe = false;

		for (var frame = 0; frame < 5000; frame++)
		{
			var before = party.ToDictionary();
			foreach (var id in party.Keys.ToList())
			{
				var member = party[id];
				var roll = random.NextDouble();
				party[id] = member.Dead
					? roll < 0.1 ? (random.NextDouble() < 0.5 ? Fighting(id) : Idle(id)) : member
					: roll < 0.08 ? Dead(id) : roll < 0.2 ? member with { InCombat = !member.InCombat } : member;
			}

			if (random.NextDouble() < 0.01 && party.Count > 1)
			{
				party.Remove(party.Keys.ElementAt(random.Next(party.Count)));
			}

			if (random.NextDouble() < 0.01 && party.Count < 4)
			{
				party[nextId] = random.NextDouble() < 0.2 ? Dead(nextId) : Idle(nextId);
				nextId++;
			}

			var members = party.Values.ToArray();
			var deathThisFrame = members.Any(member => member.Dead && before.TryGetValue(member.Id, out var was) && !was.Dead);
			deathSeenSinceWipe |= deathThisFrame;
			foreach (var member in members.Where(member => member.Dead))
			{
				everDead.Add(member.Id);
			}

			var wiped = See(frame, members);

			if (wiped)
			{
				Assert.True(deathThisFrame, $"Frame {frame}: a wipe on a frame with no death");
				Assert.DoesNotContain(members, member => member.InCombat);
				Assert.All(members, member => Assert.Contains(member.Id, everDead));
				Assert.True(deathSeenSinceWipe, $"Frame {frame}: a wipe with no death seen since the last one");
				deathSeenSinceWipe = false;
			}

			if (members.All(member => member.Dead) && deathThisFrame)
			{
				Assert.True(wiped, $"Frame {frame}: the last member died, and it wasn't a wipe");
			}
		}

		Assert.NotEmpty(_wipes);
	}
}
