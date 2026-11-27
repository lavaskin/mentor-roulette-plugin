using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Recording;

public static class PartyReading
{
	private const uint NoEntity = 0xE0000000;

	public static Party Read(IEnumerable<PartySlot> slots, PartySlot player)
	{
		var members = new List<PartyMember>();
		var seen = new HashSet<uint>();
		var notHere = 0;

		if (player.MaxHp > 0 && player.EntityId is not (0 or NoEntity))
		{
			seen.Add(player.EntityId);
			members.Add(Member(player with { OtherParty = false }));
		}

		foreach (var slot in slots)
		{
			if (slot.EntityId is 0 or NoEntity || !seen.Add(slot.EntityId))
			{
				continue;
			}

			if (slot.MaxHp == 0 || slot.TerritoryType != player.TerritoryType)
			{
				notHere++;
				continue;
			}

			members.Add(Member(slot));
		}

		return new Party(members, notHere);
	}

	private static PartyMember Member(PartySlot slot)
	{
		var dead = slot.CurrentHp == 0;
		return new PartyMember(slot.EntityId, dead, !dead && slot.InCombat, slot.OtherParty);
	}
}
