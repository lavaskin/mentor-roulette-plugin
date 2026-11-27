using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Recording;

namespace MentorRouletteTracker.Core.Tests;

public class PartyReadingTests
{
	private const uint Zone = 1036;
	private const uint Elsewhere = 129;
	private const uint Player = 0x10000001;

	private static readonly PartySlot Me = new(Player, CurrentHp: 50000, MaxHp: 50000, Zone, InCombat: false);

	private static PartySlot Slot(uint id, uint hp = 40000, uint maxHp = 40000, uint zone = Zone, bool inCombat = false, bool otherParty = false)
	{
		return new PartySlot(id, hp, maxHp, zone, inCombat, otherParty);
	}

	[Fact]
	public void ThePartysSlots_AreItsMembers()
	{
		var party = PartyReading.Read([Slot(Player, inCombat: true), Slot(2), Slot(3, hp: 0), Slot(4, inCombat: true)], Me with { InCombat = true });

		Assert.Equal(
			[
				new PartyMember(Player, Dead: false, InCombat: true),
				new PartyMember(2, Dead: false, InCombat: false),
				new PartyMember(3, Dead: true, InCombat: false),
				new PartyMember(4, Dead: false, InCombat: true),
			],
			party.Members);
		Assert.Equal(0, party.NotHere);
	}

	[Fact]
	public void TheDead_ArentInCombat()
	{
		var party = PartyReading.Read([Slot(Player), Slot(2, hp: 0, inCombat: true)], Me);

		Assert.False(party.Members[1].InCombat);
		Assert.True(party.Members[1].Dead);
	}

	[Fact]
	public void ASlotThatHasntLoaded_IsLeftOut()
	{
		var party = PartyReading.Read([Slot(Player), Slot(2, hp: 0, maxHp: 0)], Me);

		Assert.Equal([Player], party.Members.Select(member => member.Id));
		Assert.Equal(1, party.NotHere);
	}

	[Fact]
	public void AMemberInAnotherZone_IsLeftOut()
	{
		var party = PartyReading.Read([Slot(Player), Slot(2, zone: Elsewhere), Slot(3, zone: 0)], Me);

		Assert.Equal([Player], party.Members.Select(member => member.Id));
		Assert.Equal(2, party.NotHere);
	}

	[Theory]
	[InlineData(0u)]
	[InlineData(0xE0000000u)]
	public void AnEmptySlot_IsSkipped(uint id)
	{
		var party = PartyReading.Read([Slot(Player), Slot(id)], Me);

		Assert.Equal([Player], party.Members.Select(member => member.Id));
		Assert.Equal(0, party.NotHere);
	}

	[Fact]
	public void TheSameMemberTwice_IsOneMember()
	{
		var party = PartyReading.Read([Slot(Player), Slot(2), Slot(2, otherParty: true)], Me);

		Assert.Equal([Player, 2u], party.Members.Select(member => member.Id));
	}

	[Fact]
	public void WithNoParty_ThePartyIsThePlayer()
	{
		var party = PartyReading.Read([], Me with { CurrentHp = 0 });

		Assert.Equal([new PartyMember(Player, Dead: true, InCombat: false)], party.Members);
	}

	[Fact]
	public void WithNoParty_ThePlayerNotLoaded_IsNoParty()
	{
		var party = PartyReading.Read([], Me with { MaxHp = 0 });

		Assert.Empty(party.Members);
	}

	[Fact]
	public void ThePlayer_IsReadFromThemselves_NotTheirSlot()
	{
		var party = PartyReading.Read([Slot(Player, hp: 0), Slot(2)], Me with { InCombat = true });

		Assert.Equal(
			[
				new PartyMember(Player, Dead: false, InCombat: true),
				new PartyMember(2, Dead: false, InCombat: false),
			],
			party.Members);
	}

	[Fact]
	public void ThePlayersSlotLaggingBehindThem_LeavesThemIn()
	{
		var party = PartyReading.Read([Slot(Player, zone: Elsewhere), Slot(2, zone: Elsewhere)], Me);

		Assert.Equal([Player], party.Members.Select(member => member.Id));
		Assert.Equal(1, party.NotHere);
	}

	[Fact]
	public void ThePlayerNotLoaded_IsReadFromTheirSlot()
	{
		var party = PartyReading.Read([Slot(Player, inCombat: true), Slot(2)], new PartySlot(0, 0, 0, Zone, InCombat: false));

		Assert.Equal(
			[
				new PartyMember(Player, Dead: false, InCombat: true),
				new PartyMember(2, Dead: false, InCombat: false),
			],
			party.Members);
	}

	[Fact]
	public void AllianceSlots_AreInTheOtherParties()
	{
		var party = PartyReading.Read([Slot(Player), Slot(2), Slot(9, otherParty: true), Slot(10, hp: 0, otherParty: true)], Me);

		Assert.Equal([false, false, true, true], party.Members.Select(member => member.OtherParty));
	}

	[Fact]
	public void InAnAllianceWithNoPartySlots_ThePlayerIsStillThere()
	{
		var party = PartyReading.Read([Slot(9, otherParty: true)], Me);

		Assert.Equal([Player, 9u], party.Members.Select(member => member.Id));
	}
}
