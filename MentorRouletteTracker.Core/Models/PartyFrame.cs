namespace MentorRouletteTracker.Core.Models;

/// <summary>
/// What one frame of the party adds to the run
/// </summary>
/// <param name="Deaths">Members seen going from alive to dead, in every party of an alliance</param>
/// <param name="Wipe">Where the wipe this frame finished was counted from, or null if it didn't finish one that wasn't already counted</param>
/// <param name="WipeMembers">Everyone who had to die for the wipe</param>
public readonly record struct PartyFrame(int Deaths, WipeSource? Wipe = null, int WipeMembers = 0);
