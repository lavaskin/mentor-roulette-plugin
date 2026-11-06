namespace MentorRouletteTracker.Core.Uploading;

/// <summary>
/// The upload settings, which the plugin's configuration holds
/// </summary>
public interface IUploadSettings
{
	string ServerAddress { get; }

	/// <summary>
	/// The plugin token, or null when not connected
	/// </summary>
	string? Token { get; }

	bool AutoUpload { get; }
}
