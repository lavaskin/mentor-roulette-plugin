using System.Text.Json;
using System.Text.Json.Serialization;

namespace MentorRouletteTracker.Core.Storage;

public static class JsonFile
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
	};

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

			stream.Flush(flushToDisk: true);
		}

		File.Move(temporary, path, overwrite: true);
	}
}
