namespace MentorRouletteTracker.Core.Models;

/// <summary>
/// The duty finder's pop, as it holds it once accepted
/// </summary>
/// <param name="IsMentorRoulette">It's the Mentor Roulette (a roulette, <c>ContentRoulette</c> 9)</param>
/// <param name="InProgress">It's a duty in progress, so the player is a replacement</param>
public sealed record AcceptedPop(bool IsMentorRoulette, bool InProgress);
