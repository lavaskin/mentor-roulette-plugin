using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker.Core.Tests;

public class MilestonesTests
{
	private static readonly SeenTotals Done = new(16, 16);
	private static readonly SeenTotals NotDone = new(10, 16);

	private static NewDuty Found(
		int seen = 143,
		SeenTotals? ofType = null,
		SeenTotals? ofTypeInExpansion = null,
		string? type = "Dungeon",
		string? expansion = "3.0: Heavensward") =>
		new(seen, 186, ofType ?? NotDone, ofTypeInExpansion ?? NotDone, type, expansion);

	[Fact]
	public void ANewDuty_ThatFinishesNothing_IsFound()
	{
		Assert.Equal("New duty found!", Milestones.Found(Found()));
	}

	[Fact]
	public void FinishingTheTypeInItsExpansion_NamesBoth()
	{
		Assert.Equal("Every Heavensward dungeon seen!", Milestones.Found(Found(ofTypeInExpansion: Done)));
	}

	[Fact]
	public void FinishingTheType_NamesIt_InLowerCase()
	{
		Assert.Equal("Every extreme trial seen!", Milestones.Found(Found(ofType: Done, type: "Extreme Trial")));
	}

	[Fact]
	public void FinishingTheRoulette_SaysSo()
	{
		Assert.Equal("Every duty in the roulette seen!", Milestones.Found(Found(seen: 186)));
	}

	[Fact]
	public void OnlyTheBiggestMilestone_IsGiven()
	{
		Assert.Equal("Every dungeon seen!", Milestones.Found(Found(ofType: Done, ofTypeInExpansion: Done)));
		Assert.Equal("Every duty in the roulette seen!", Milestones.Found(Found(seen: 186, ofType: Done, ofTypeInExpansion: Done)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ADutyWithNoType_CanOnlyFinishTheRoulette(string? type)
	{
		Assert.Equal("New duty found!", Milestones.Found(Found(ofType: Done, ofTypeInExpansion: Done, type: type)));
		Assert.Equal("Every duty in the roulette seen!", Milestones.Found(Found(seen: 186, type: type)));
	}

	[Fact]
	public void ADutyWithNoExpansion_CanStillFinishItsType()
	{
		Assert.Equal("New duty found!", Milestones.Found(Found(ofTypeInExpansion: Done, expansion: null)));
		Assert.Equal("Every dungeon seen!", Milestones.Found(Found(ofType: Done, expansion: null)));
	}

	[Fact]
	public void AnExpansionWithoutItsNumber_IsUsedAsItIs()
	{
		Assert.Equal("Every Dawntrail dungeon seen!", Milestones.Found(Found(ofTypeInExpansion: Done, expansion: "Dawntrail")));
	}

	[Fact]
	public void NoneOfNone_IsntFinished()
	{
		Assert.Equal("New duty found!", Milestones.Found(new NewDuty(0, 0, new SeenTotals(0, 0), new SeenTotals(0, 0), "Dungeon", "3.0: Heavensward")));
	}

	[Fact]
	public void SeveralRuns_CountTheirNewDuties()
	{
		Assert.Null(Milestones.Found([]));
		Assert.Equal("New duty found!", Milestones.Found([Found()]));
		Assert.Equal("2 new duties found!", Milestones.Found([Found(), Found()]));
	}

	[Fact]
	public void SeveralRuns_GiveTheBiggestMilestoneAnyOfThemFinished()
	{
		Assert.Equal(
			"Every dungeon seen!",
			Milestones.Found([Found(ofTypeInExpansion: Done), Found(ofType: Done), Found()]));
	}

	[Fact]
	public void OfTwoMilestonesAsBig_TheLaterOneIsGiven()
	{
		Assert.Equal(
			"Every Stormblood trial seen!",
			Milestones.Found([Found(ofTypeInExpansion: Done), Found(ofTypeInExpansion: Done, type: "Trial", expansion: "4.0: Stormblood")]));
	}
}
