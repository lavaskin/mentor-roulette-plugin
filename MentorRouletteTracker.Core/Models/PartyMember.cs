namespace MentorRouletteTracker.Core.Models;

public readonly record struct PartyMember(uint Id, bool Dead, bool InCombat, bool OtherParty = false);
