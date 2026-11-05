using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Storage;

namespace MentorRouletteTracker.Core.Tests;

public sealed class RunStoreTests : IDisposable
{
	private readonly string _directory = Path.Combine(Path.GetTempPath(), "mrt-tests-" + Guid.NewGuid());
	private readonly List<string> _warnings = [];
	private readonly RunStore _store;

	public RunStoreTests()
	{
		_store = new RunStore(_directory, _warnings.Add);
	}

	public void Dispose()
	{
		Directory.Delete(_directory, recursive: true);
	}

	[Fact]
	public void TheRuns_ReadBackAsWritten()
	{
		var uploaded = Runs.Make(hour: 2, status: RunStatus.Uploaded) with { SiteRunId = 5123, RunNumber = 123 };
		var failed = Runs.Make(hour: 1, status: RunStatus.Failed) with { FailedReason = "That duty isn't in the Mentor Roulette.", Replacement = true, Wipes = 3 };
		var waiting = Runs.Make(hour: 0) with { Cleared = false };

		_store.SaveRuns([uploaded, failed, waiting]);

		Assert.Equal([uploaded, failed, waiting], new RunStore(_directory, _warnings.Add).LoadRuns());
		Assert.Equal(DateTimeKind.Utc, _store.LoadRuns()[0].DatePlayed.Kind);
	}

	[Fact]
	public void TheRunInProgress_ReadsBackAsWritten()
	{
		var run = new InProgressRun
		{
			Id = Guid.NewGuid(),
			GameContentId = 421,
			GameClassJobId = 19,
			Replacement = true,
			Wipes = 2,
			ElapsedSeconds = 612.25,
			LastSeenAt = Runs.Day.AddHours(3),
		};

		_store.SaveInProgress(run);

		Assert.Equal(run, _store.LoadInProgress());
	}

	[Fact]
	public void NoRunInProgress_DeletesItsFile()
	{
		_store.SaveInProgress(new InProgressRun
		{
			Id = Guid.NewGuid(),
			GameContentId = 421,
			GameClassJobId = 0,
			Replacement = false,
			Wipes = 0,
			ElapsedSeconds = 1,
			LastSeenAt = Runs.Day,
		});

		_store.SaveInProgress(null);

		Assert.Null(_store.LoadInProgress());
		Assert.False(File.Exists(Path.Combine(_directory, RunStore.InProgressFileName)));
	}

	[Fact]
	public void NothingSaved_ReadsAsNothing()
	{
		Assert.Empty(_store.LoadRuns());
		Assert.Null(_store.LoadInProgress());
		Assert.Empty(_warnings);
	}

	[Fact]
	public void AFileThatCantBeRead_IsSetAside_NotOverwritten()
	{
		var path = Path.Combine(_directory, RunStore.RunsFileName);
		File.WriteAllText(path, "{ not json");

		Assert.Empty(_store.LoadRuns());

		Assert.False(File.Exists(path));
		var setAside = Assert.Single(Directory.GetFiles(_directory, RunStore.RunsFileName + ".unreadable-*"));
		Assert.Equal("{ not json", File.ReadAllText(setAside));
		Assert.Single(_warnings);
	}

	[Fact]
	public void TheFile_HoldsNoTemporaryFileAfterwards()
	{
		_store.SaveRuns([Runs.Make(hour: 1)]);

		Assert.Equal([RunStore.RunsFileName], Directory.GetFiles(_directory).Select(Path.GetFileName));
	}
}
