namespace MentorRouletteTracker.Core.Models;

public readonly record struct PartyFrame(int Deaths, WipeSource? Wipe = null, int WipeMembers = 0);
