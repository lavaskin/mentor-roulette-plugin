namespace MentorRouletteTracker.Core.Uploading;

public interface IUploadSettings
{
	string ServerAddress { get; }

	string? Token { get; }

	bool AutoUpload { get; }
}
