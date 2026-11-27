namespace MentorRouletteTracker.Core.Models;

/// <summary>
/// Everyone in the party in one frame, the other parties of an alliance included
/// </summary>
/// <param name="Members">The members who count: loaded, and in the player's zone</param>
/// <param name="NotHere">Members left out, since they haven't loaded or aren't in the player's zone (disconnected, say)</param>
public sealed record Party(IReadOnlyList<PartyMember> Members, int NotHere = 0);
