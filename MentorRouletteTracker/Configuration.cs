using System;
using Dalamud.Configuration;
using MentorRouletteTracker.Core.Uploading;

namespace MentorRouletteTracker;

/// <summary>
/// The plugin's settings, which Dalamud keeps in its config file. One connection covers every character on the install.
/// </summary>
[Serializable]
public sealed class Configuration : IPluginConfiguration, IUploadSettings
{
	public int Version { get; set; } = 1;

	public string ServerAddress { get; set; } = Core.Api.ServerAddress.Default;

	/// <summary>
	/// The plugin token from the site's Account page, or null when not connected
	/// </summary>
	public string? Token { get; set; }

	/// <summary>
	/// Whose account the token is for, from when it connected
	/// </summary>
	public string? Username { get; set; }

	public bool AutoUpload { get; set; } = true;

	/// <summary>
	/// A line in chat when a run ends
	/// </summary>
	public bool ChatLine { get; set; } = true;
}
