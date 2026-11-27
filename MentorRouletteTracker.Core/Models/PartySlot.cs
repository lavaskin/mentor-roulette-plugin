namespace MentorRouletteTracker.Core.Models;

public readonly record struct PartySlot(uint EntityId, uint CurrentHp, uint MaxHp, uint TerritoryType, bool InCombat, bool OtherParty = false);
