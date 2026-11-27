namespace MentorRouletteTracker.Core.Models;

public sealed record Party(IReadOnlyList<PartyMember> Members, int NotHere = 0);
