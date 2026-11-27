using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Recording;

/// <summary>
/// Which of the game's party slots count for the party watcher
/// </summary>
public static class PartyReading
{
	/// <summary>
	/// The game's id for no one
	/// </summary>
	private const uint NoEntity = 0xE0000000;

	/// <summary>
	/// The party in one frame, from the game's party and alliance slots. A slot counts if it's loaded (max HP above 0)
	/// and in the player's zone. Anyone else is left out, so they don't have to die for a wipe: a disconnected member
	/// keeps their slot, with whatever HP they had. With no party at all (the player's on their own), the party is
	/// just the player.
	/// </summary>
	/// <param name="slots">The party's slots, then the alliance's</param>
	/// <param name="player">The player, as a slot, with the zone they're in</param>
	public static Party Read(IEnumerable<PartySlot> slots, PartySlot player)
	{
		var members = new List<PartyMember>();
		var seen = new HashSet<uint>();
		var notHere = 0;
		var ownParty = false;

		foreach (var slot in slots)
		{
			if (slot.EntityId is 0 or NoEntity || !seen.Add(slot.EntityId))
			{
				continue;
			}

			ownParty |= !slot.OtherParty;
			if (slot.MaxHp == 0 || slot.TerritoryType != player.TerritoryType)
			{
				notHere++;
				continue;
			}

			members.Add(Member(slot));
		}

		if (!ownParty && player.MaxHp > 0 && seen.Add(player.EntityId))
		{
			members.Insert(0, Member(player with { OtherParty = false }));
		}

		return new Party(members, notHere);
	}

	private static PartyMember Member(PartySlot slot)
	{
		var dead = slot.CurrentHp == 0;
		return new PartyMember(slot.EntityId, dead, !dead && slot.InCombat, slot.OtherParty);
	}
}
