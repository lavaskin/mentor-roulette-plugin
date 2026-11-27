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
		var waiting = Runs.Make(hour: 0) with { Cleared = false, Deaths = 7 };

		_store.SaveRuns([uploaded, failed, waiting]);

		Assert.Equal([uploaded, failed, waiting], new RunStore(_directory, _warnings.Add).LoadRuns());
		Assert.Equal(DateTimeKind.Utc, _store.LoadRuns()[0].DatePlayed.Kind);
	}

	[Fact]
	public void TheRuns_ReadBackAsTheyAre_AfterADelete()
	{
		var waiting = Runs.Make(hour: 1);
		var log = new RunLog([waiting, Runs.Make(hour: 2, status: RunStatus.Uploaded) with { SiteRunId = 5123, RunNumber = 123 }]);
		_store.SaveRuns(log.Runs);

		log.DeleteUploaded();
		_store.SaveRuns(log.Runs);

		Assert.Equal([waiting], new RunStore(_directory, _warnings.Add).LoadRuns());
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
			Deaths = 9,
			ElapsedSeconds = 612.25,
			EnteredAt = Runs.Day.AddHours(2).AddMinutes(50),
			LastSeenAt = Runs.Day.AddHours(3),
		};

		_store.SaveInProgress(run);

		Assert.Equal(run, _store.LoadInProgress());
	}

	[Fact]
	public void ARunInProgress_SavedWithoutItsEntryTime_ReadsWithNone()
	{
		File.WriteAllText(Path.Combine(_directory, RunStore.InProgressFileName), """
			{
			  "id": "6f1f8d0e-3c1a-4a51-9a51-6f0a2b1c9d11",
			  "gameContentId": 421,
			  "gameClassJobId": 19,
			  "replacement": false,
			  "wipes": 1,
			  "elapsedSeconds": 600,
			  "lastSeenAt": "2026-11-05T03:10:00Z",
			  "awaitingDutyCommenced": false
			}
			""");

		var run = _store.LoadInProgress();

		Assert.Null(run!.EnteredAt);
		Assert.Equal(600, run.ElapsedSeconds);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void ARunInProgress_SavedWithoutDeaths_ReadsWithNone()
	{
		File.WriteAllText(Path.Combine(_directory, RunStore.InProgressFileName), """
			{
			  "id": "6f1f8d0e-3c1a-4a51-9a51-6f0a2b1c9d11",
			  "gameContentId": 421,
			  "gameClassJobId": 19,
			  "replacement": false,
			  "wipes": 1,
			  "elapsedSeconds": 600,
			  "enteredAt": "2026-11-05T03:00:00Z",
			  "lastSeenAt": "2026-11-05T03:10:00Z",
			  "awaitingDutyCommenced": false,
			  "pluginTurnedOff": false,
			  "clockRanWhileOff": false
			}
			""");

		var run = _store.LoadInProgress();

		Assert.Null(run!.Deaths);
		Assert.Equal(1, run.Wipes);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void RunsSavedWithoutDeaths_ReadWithNone()
	{
		File.WriteAllText(Path.Combine(_directory, RunStore.RunsFileName), """
			{
			  "version": 1,
			  "runs": [
			    {
			      "id": "6f1f8d0e-3c1a-4a51-9a51-6f0a2b1c9d11",
			      "gameContentId": 421,
			      "gameClassJobId": 19,
			      "cleared": true,
			      "replacement": false,
			      "wipes": 2,
			      "durationSeconds": 1930,
			      "datePlayed": "2026-11-05T03:00:00Z",
			      "status": "uploaded",
			      "siteRunId": 5123,
			      "runNumber": 123,
			      "failedReason": null
			    }
			  ]
			}
			""");

		var run = Assert.Single(_store.LoadRuns());

		Assert.Null(run.Deaths);
		Assert.Equal(2, run.Wipes);
		Assert.Empty(_warnings);
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
