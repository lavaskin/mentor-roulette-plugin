using System.Text.Json;
using System.Text.Json.Serialization;
using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Storage;

/// <summary>
/// The plugin's runs on disk, as JSON in its config directory: <c>runs.json</c> (every run, kept forever) and
/// <c>run-in-progress.json</c> (the run being played, while there is one). Each file is written whole to a temporary
/// file, flushed to disk, and then moved over the old one, so a crash mid-write can't leave it half written.
/// </summary>
public sealed class RunStore
{
	public const string RunsFileName = "runs.json";
	public const string InProgressFileName = "run-in-progress.json";

	private const int Version = 1;

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
	};

	private readonly string _runsPath;
	private readonly string _inProgressPath;
	private readonly Action<string> _warn;

	/// <param name="warn">Told about a file that couldn't be read, which is set aside rather than overwritten</param>
	public RunStore(string directory, Action<string> warn)
	{
		Directory.CreateDirectory(directory);
		_runsPath = Path.Combine(directory, RunsFileName);
		_inProgressPath = Path.Combine(directory, InProgressFileName);
		_warn = warn;
	}

	public IReadOnlyList<Run> LoadRuns()
	{
		return Read<RunsFile>(_runsPath)?.Runs ?? [];
	}

	public InProgressRun? LoadInProgress()
	{
		return Read<InProgressRun>(_inProgressPath);
	}

	public void SaveRuns(IReadOnlyList<Run> runs)
	{
		Write(_runsPath, new RunsFile { Version = Version, Runs = runs });
	}

	/// <summary>
	/// Saves the run in progress, or with null deletes the file: there isn't one.
	/// </summary>
	public void SaveInProgress(InProgressRun? run)
	{
		if (run is null)
		{
			File.Delete(_inProgressPath);
		}
		else
		{
			Write(_inProgressPath, run);
		}
	}

	private T? Read<T>(string path) where T : class
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
			_warn($"Couldn't read {Path.GetFileName(path)}, so it was moved to {Path.GetFileName(setAside)}: {exception.Message}");
			return null;
		}
	}

	private static void Write<T>(string path, T value)
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

	private sealed record RunsFile
	{
		public required int Version { get; init; }
		public required IReadOnlyList<Run> Runs { get; init; }
	}
}
