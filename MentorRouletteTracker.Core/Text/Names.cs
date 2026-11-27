namespace MentorRouletteTracker.Core.Text;

public static class Names
{
	public static string Capitalize(string name)
	{
		return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
	}
}
