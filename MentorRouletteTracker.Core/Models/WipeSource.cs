namespace MentorRouletteTracker.Core.Models;

/// <summary>
/// Where a wipe was counted from
/// </summary>
public enum WipeSource
{
	/// <summary>
	/// Everyone in the party died during the same fight
	/// </summary>
	Party,

	/// <summary>
	/// The game's wipe message, when it resets the whole party
	/// </summary>
	GameMessage,
}
