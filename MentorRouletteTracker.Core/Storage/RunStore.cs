using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Storage;

/// <summary>
/// The plugin's runs on disk, as JSON files (<see cref="JsonFile"/>) in its data directory: <c>runs.json</c> (every
/// run, kept forever) and <c>run-in-progress.json</c> (the run being played, while there is one).
/// </summary>
public sealed class RunStore
{
	public const string RunsFileName = "runs.json";
	public const string InProgressFileName = "run-in-progress.json";

	private const int Version = 1;

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
		return JsonFile.Read<RunsFile>(_runsPath, _warn)?.Runs ?? [];
	}

	public InProgressRun? LoadInProgress()
	{
		return JsonFile.Read<InProgressRun>(_inProgressPath, _warn);
	}

	public void SaveRuns(IReadOnlyList<Run> runs)
	{
		JsonFile.Write(_runsPath, new RunsFile { Version = Version, Runs = runs });
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
			JsonFile.Write(_inProgressPath, run);
		}
	}

	private sealed record RunsFile
	{
		public required int Version { get; init; }
		public required IReadOnlyList<Run> Runs { get; init; }
	}
}
