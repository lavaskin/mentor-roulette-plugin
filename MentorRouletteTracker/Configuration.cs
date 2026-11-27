using System;
using Dalamud.Configuration;
using MentorRouletteTracker.Core.Uploading;

namespace MentorRouletteTracker;

[Serializable]
public sealed class Configuration : IPluginConfiguration, IUploadSettings
{
	public int Version { get; set; } = 1;

	public string ServerAddress { get; set; } = Core.Api.ServerAddress.Default;

	public string? Token { get; set; }

	public string? Username { get; set; }

	public bool AutoUpload { get; set; } = true;

	public bool ChatLine { get; set; } = true;

	public bool ChatLinePace { get; set; } = true;
}
