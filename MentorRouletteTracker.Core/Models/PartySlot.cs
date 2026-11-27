namespace MentorRouletteTracker.Core.Models;

/// <summary>
/// A slot of the game's party or alliance list, as read, before it's known whether it counts
/// </summary>
/// <param name="EntityId">The game's object id for whoever's in it</param>
/// <param name="MaxHp">0 for a slot that hasn't loaded</param>
/// <param name="TerritoryType">The zone they're in</param>
/// <param name="InCombat">In combat, as far as the game shows: false for someone not showing</param>
/// <param name="OtherParty">An alliance slot: in another party of the alliance</param>
public readonly record struct PartySlot(uint EntityId, uint CurrentHp, uint MaxHp, uint TerritoryType, bool InCombat, bool OtherParty = false);
