using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Storage;

/// <summary>
/// Every run the plugin has recorded, newest first. Thread safe: the windows read it each frame while uploads
/// change it in the background.
/// </summary>
public sealed class RunLog
{
	private readonly Lock _lock = new();
	private IReadOnlyList<Run> _runs;

	public RunLog(IEnumerable<Run> runs)
	{
		_runs = Sorted(runs);
	}

	/// <summary>
	/// Raised after every change, on the thread that made it
	/// </summary>
	public event Action? Changed;

	/// <summary>
	/// The runs, newest first. A snapshot: it doesn't change once read.
	/// </summary>
	public IReadOnlyList<Run> Runs => Volatile.Read(ref _runs);

	/// <summary>
	/// The runs waiting to upload, in the order they upload: oldest first. Failed runs are set aside, so they aren't.
	/// </summary>
	public IReadOnlyList<Run> NotUploaded => Runs
		.Where(run => run.Status == RunStatus.NotUploaded)
		.OrderBy(run => run.DatePlayed)
		.ToList();

	public Run? Find(Guid id) => Runs.FirstOrDefault(run => run.Id == id);

	public void Add(Run run)
	{
		Change(runs => runs.Any(existing => existing.Id == run.Id) ? runs : runs.Append(run));
	}

	/// <summary>
	/// Replaces a run with <paramref name="change"/>'s copy of it. Nothing happens if it's gone.
	/// </summary>
	public void Update(Guid id, Func<Run, Run> change)
	{
		Change(runs => runs.Select(run => run.Id == id ? change(run) : run));
	}

	/// <summary>
	/// Deletes a run that isn't uploaded. Uploaded runs are kept, since they're fixed on the site.
	/// </summary>
	/// <returns>Whether it was deleted</returns>
	public bool Delete(Guid id)
	{
		var deleted = false;
		Change(runs =>
		{
			var kept = runs.Where(run => run.Id != id || run.Status == RunStatus.Uploaded).ToList();
			deleted = kept.Count < runs.Count();
			return kept;
		});
		return deleted;
	}

	private void Change(Func<IEnumerable<Run>, IEnumerable<Run>> change)
	{
		lock (_lock)
		{
			Volatile.Write(ref _runs, Sorted(change(_runs)));
		}

		Changed?.Invoke();
	}

	private static List<Run> Sorted(IEnumerable<Run> runs)
	{
		return runs.OrderByDescending(run => run.DatePlayed).ToList();
	}
}
