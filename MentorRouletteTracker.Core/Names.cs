namespace MentorRouletteTracker.Core;

public static class Names
{
	/// <summary>
	/// The game's names for duties and jobs start in lower case in English ("the Vault", "paladin"), since it puts
	/// them mid-sentence. Shown on their own, they start with a capital: "The Vault", "Paladin".
	/// </summary>
	public static string Capitalize(string name)
	{
		return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
	}
}
