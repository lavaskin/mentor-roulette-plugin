using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Storage;

public sealed class RunLog
{
	private readonly Lock _lock = new();
	private IReadOnlyList<Run> _runs;

	public RunLog(IEnumerable<Run> runs)
	{
		_runs = Sorted(runs);
	}

	public event Action? Changed;

	public IReadOnlyList<Run> Runs => Volatile.Read(ref _runs);

	public IReadOnlyList<Run> NotUploaded => Runs
		.Where(run => run.Status == RunStatus.NotUploaded)
		.OrderBy(run => run.DatePlayed)
		.ToList();

	public Run? Find(Guid id) => Runs.FirstOrDefault(run => run.Id == id);

	public void Add(Run run)
	{
		Change(runs => runs.Any(existing => existing.Id == run.Id) ? runs : runs.Append(run));
	}

	public void Update(Guid id, Func<Run, Run> change)
	{
		Change(runs => runs.Select(run => run.Id == id ? change(run) : run));
	}

	public bool Delete(Guid id)
	{
		return Remove(run => run.Id == id && run.Status != RunStatus.Uploaded) > 0;
	}

	public int DeleteUploaded()
	{
		return Remove(run => run.Status == RunStatus.Uploaded);
	}

	public int DeleteAll()
	{
		return Remove(_ => true);
	}

	private int Remove(Func<Run, bool> deleting)
	{
		var deleted = 0;
		Change(runs =>
		{
			var all = runs.ToList();
			var kept = all.Where(run => !deleting(run)).ToList();
			deleted = all.Count - kept.Count;
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
