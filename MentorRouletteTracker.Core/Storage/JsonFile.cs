using System.Text.Json;
using System.Text.Json.Serialization;

namespace MentorRouletteTracker.Core.Storage;

/// <summary>
/// The plugin's own JSON files. Each is written whole to a temporary file, flushed to disk, and then moved over the
/// old one, so a crash mid-write can't leave it half written. One that can't be read is set aside, not overwritten.
/// </summary>
public static class JsonFile
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
	};

	/// <returns>The file's contents, or null when there's no file, or it couldn't be read</returns>
	/// <param name="warn">Told about a file that couldn't be read, which is set aside rather than overwritten</param>
	public static T? Read<T>(string path, Action<string> warn) where T : class
	{
		if (!File.Exists(path))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
		}
		catch (JsonException exception)
		{
			// Kept, so nothing's lost: starting over would otherwise overwrite it on the next save
			var setAside = $"{path}.unreadable-{DateTime.UtcNow:yyyyMMddHHmmss}";
			File.Move(path, setAside);
			warn($"Couldn't read {Path.GetFileName(path)}, so it was moved to {Path.GetFileName(setAside)}: {exception.Message}");
			return null;
		}
	}

	public static void Write<T>(string path, T value)
	{
		var temporary = path + ".tmp";
		using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
		{
			JsonSerializer.Serialize(stream, value, JsonOptions);

			// On disk before it replaces the old file, so a power cut can't leave an empty one in its place
			stream.Flush(flushToDisk: true);
		}

		File.Move(temporary, path, overwrite: true);
	}
}
