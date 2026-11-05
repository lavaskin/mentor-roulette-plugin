using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker.Core.Tests;

public class TextTests
{
	[Theory]
	[InlineData("the Vault", "The Vault")]
	[InlineData("paladin", "Paladin")]
	[InlineData("Sastasha", "Sastasha")]
	[InlineData("", "")]
	public void Names_StartWithACapital(string name, string expected)
	{
		Assert.Equal(expected, Names.Capitalize(name));
	}

	[Fact]
	public void Dates_ReadLikeTheSites_InTheGivenTimeZone()
	{
		var utc = new DateTime(2026, 11, 5, 3, 12, 44, DateTimeKind.Utc);

		Assert.Equal("Nov 5, 2026, 3:12 AM", DateText.Format(utc, TimeZoneInfo.Utc));
		Assert.Equal("Nov 4, 2026, 10:12 PM", DateText.Format(utc, TimeZoneInfo.CreateCustomTimeZone("UTC-5", TimeSpan.FromHours(-5), "UTC-5", "UTC-5")));
	}

	[Theory]
	[InlineData("https://mentor.lavask.in", "https://mentor.lavask.in")]
	[InlineData("  https://mentor.lavask.in/  ", "https://mentor.lavask.in")]
	[InlineData("http://localhost:4200", "http://localhost:4200")]
	[InlineData("http://localhost:4200/mrt/", "http://localhost:4200/mrt")]
	[InlineData("mentor.lavask.in", null)]
	[InlineData("ftp://mentor.lavask.in", null)]
	[InlineData("https://mentor.lavask.in/?a=1", null)]
	[InlineData("", null)]
	public void ServerAddresses_AreWebAddresses(string text, string? expected)
	{
		Assert.Equal(expected, ServerAddress.Normalize(text));
	}

	[Fact]
	public void Links_GoUnderTheServerAddress()
	{
		Assert.Equal("https://mentor.lavask.in/roulettes?run=12", ServerAddress.Link("https://mentor.lavask.in/", "/roulettes?run=12"));
	}
}
