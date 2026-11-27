using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker.Core.Models;

public class Settings
{
	public int Version { get; set; } = 1;

	public string ServerAddress { get; set; } = Api.ServerAddress.Default;

	public string? Token { get; set; }

	public string? Username { get; set; }

	public bool AutoUpload { get; set; } = true;

	public bool ChatLine { get; set; } = true;

	public bool ChatLinePace { get; set; } = true;
}
