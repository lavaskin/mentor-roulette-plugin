using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker.Core.Text;

public static class Milestones
{
	private const string NewDutyFound = "New duty found!";

	private enum Scope
	{
		TypeInExpansion,
		Type,
		Roulette,
	}

	public static string Found(NewDuty newDuty)
	{
		return Milestone(newDuty)?.Text ?? NewDutyFound;
	}

	public static string? Found(IReadOnlyList<NewDuty> newDuties)
	{
		var biggest = newDuties
			.Select(Milestone)
			.OfType<(Scope Scope, string Text)>()
			.OrderBy(milestone => milestone.Scope)
			.Select(milestone => milestone.Text)
			.LastOrDefault();

		return biggest ?? newDuties.Count switch
		{
			0 => null,
			1 => NewDutyFound,
			var count => $"{count} new duties found!",
		};
	}

	private static (Scope Scope, string Text)? Milestone(NewDuty newDuty)
	{
		if (new SeenTotals(newDuty.DutiesSeen, newDuty.DutiesInRoulette).IsFinished)
		{
			return (Scope.Roulette, "Every duty in the roulette seen!");
		}

		if (string.IsNullOrWhiteSpace(newDuty.DutyType))
		{
			return null;
		}

		var type = newDuty.DutyType.Trim().ToLowerInvariant();
		if (newDuty.OfType?.IsFinished == true)
		{
			return (Scope.Type, $"Every {type} seen!");
		}

		if (ExpansionName(newDuty.Expansion) is { } expansion && newDuty.OfTypeInExpansion?.IsFinished == true)
		{
			return (Scope.TypeInExpansion, $"Every {expansion} {type} seen!");
		}

		return null;
	}

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
