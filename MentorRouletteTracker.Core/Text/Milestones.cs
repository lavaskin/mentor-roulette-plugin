using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker.Core.Text;

/// <summary>
/// What logging new duties found, in the site's words for its toast: "New duty found!", or the milestone a run
/// finished, "Every duty in the roulette seen!", "Every dungeon seen!" or "Every Heavensward dungeon seen!". Only the
/// biggest is given, since it includes the smaller ones.
/// </summary>
public static class Milestones
{
	/// <summary>
	/// What one new duty found
	/// </summary>
	public static string Found(NewDuty newDuty)
	{
		return Milestone(newDuty)?.Text ?? "New duty found!";
	}

	/// <summary>
	/// What several runs found together: the biggest milestone any of them finished, else how many new duties they
	/// found. Null when they found none.
	/// </summary>
	/// <param name="newDuties">In the order they were logged, so of two milestones as big, the later one's counts are
	/// the newest</param>
	public static string? Found(IReadOnlyList<NewDuty> newDuties)
	{
		var biggest = newDuties
			.Select(Milestone)
			.OfType<(int Size, string Text)>()
			.OrderBy(milestone => milestone.Size)
			.Select(milestone => milestone.Text)
			.LastOrDefault();

		return biggest ?? newDuties.Count switch
		{
			0 => null,
			1 => "New duty found!",
			var count => $"{count} new duties found!",
		};
	}

	/// <summary>
	/// A duty with no type can only finish the whole roulette
	/// </summary>
	private static (int Size, string Text)? Milestone(NewDuty newDuty)
	{
		if (new SeenTotals(newDuty.DutiesSeen, newDuty.DutiesInRoulette).IsFinished)
		{
			return (3, "Every duty in the roulette seen!");
		}

		if (string.IsNullOrWhiteSpace(newDuty.DutyType))
		{
			return null;
		}

		var type = newDuty.DutyType.Trim().ToLowerInvariant();
		if (newDuty.OfType?.IsFinished == true)
		{
			return (2, $"Every {type} seen!");
		}

		if (ExpansionName(newDuty.Expansion) is { } expansion && newDuty.OfTypeInExpansion?.IsFinished == true)
		{
			return (1, $"Every {expansion} {type} seen!");
		}

		return null;
	}

	/// <summary>
	/// "Heavensward" from the site's "3.0: Heavensward", as the site's toast writes it
	/// </summary>
	private static string? ExpansionName(string? label)
	{
		if (string.IsNullOrWhiteSpace(label))
		{
			return null;
		}

		var colon = label.IndexOf(": ", StringComparison.Ordinal);
		return (colon < 0 ? label : label[(colon + 2)..]).Trim();
	}
}
