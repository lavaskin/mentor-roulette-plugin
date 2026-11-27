namespace MentorRouletteTracker.Core.Models;

/// <summary>
/// A member of the party, or of the alliance, as one frame shows them
/// </summary>
/// <param name="Id">The game's object id for them (<c>EntityId</c>), held in memory only and never logged, saved or sent</param>
/// <param name="Dead">HP 0</param>
/// <param name="InCombat">Alive and in combat. Someone who isn't showing in the game is taken as out of combat.</param>
/// <param name="OtherParty">In another party of the alliance, not the player's own</param>
public readonly record struct PartyMember(uint Id, bool Dead, bool InCombat, bool OtherParty = false);
