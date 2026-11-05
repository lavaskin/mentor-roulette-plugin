namespace MentorRouletteTracker.Core.Tests;

public class RunLogTests
{
	[Fact]
	public void Runs_AreNewestFirst()
	{
		var older = Runs.Make(hour: 1);
		var newer = Runs.Make(hour: 2);
		var log = new RunLog([older]);

		log.Add(newer);

		Assert.Equal([newer, older], log.Runs);
	}

	[Fact]
	public void ARun_IsOnlyAddedOnce()
	{
		var run = Runs.Make(hour: 1);
		var log = new RunLog([run]);

		log.Add(run);

		Assert.Single(log.Runs);
	}

	[Fact]
	public void NotUploaded_IsOldestFirst_WithoutFailedOrUploadedRuns()
	{
		var newer = Runs.Make(hour: 3);
		var older = Runs.Make(hour: 1);
		var log = new RunLog([newer, Runs.Make(hour: 2, status: RunStatus.Failed), older, Runs.Make(hour: 0, status: RunStatus.Uploaded)]);

		Assert.Equal([older, newer], log.NotUploaded);
	}

	[Fact]
	public void ARunNotUploaded_CanBeDeleted()
	{
		var waiting = Runs.Make(hour: 1);
		var failed = Runs.Make(hour: 2, status: RunStatus.Failed);
		var log = new RunLog([waiting, failed]);

		Assert.True(log.Delete(waiting.Id));
		Assert.True(log.Delete(failed.Id));

		Assert.Empty(log.Runs);
	}

	[Fact]
	public void AnUploadedRun_CantBeDeleted()
	{
		var uploaded = Runs.Make(hour: 1, status: RunStatus.Uploaded);
		var log = new RunLog([uploaded]);

		Assert.False(log.Delete(uploaded.Id));

		Assert.Single(log.Runs);
	}

	[Fact]
	public void EveryChange_IsAnnounced()
	{
		var run = Runs.Make(hour: 1);
		var log = new RunLog([]);
		var changes = 0;
		log.Changed += () => changes++;

		log.Add(run);
		log.Update(run.Id, current => current with { Wipes = 4 });
		log.Delete(run.Id);

		Assert.Equal(3, changes);
	}

	[Fact]
	public void Update_ReplacesTheRun()
	{
		var run = Runs.Make(hour: 1);
		var log = new RunLog([run]);

		log.Update(run.Id, current => current with { Status = RunStatus.Uploaded, RunNumber = 7 });

		Assert.Equal(7, log.Find(run.Id)!.RunNumber);
	}
}
