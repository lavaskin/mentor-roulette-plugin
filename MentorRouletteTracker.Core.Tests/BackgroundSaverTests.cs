namespace MentorRouletteTracker.Core.Tests;

public class BackgroundSaverTests
{
	[Fact]
	public void TheLastRequest_IsAlwaysSaved()
	{
		var value = 0;
		var saved = -1;
		var saver = new BackgroundSaver(() => saved = Volatile.Read(ref value), exception => throw exception);

		Parallel.For(1, 1001, i =>
		{
			Interlocked.Exchange(ref value, i);
			saver.Request();
		});
		Volatile.Write(ref value, 5000);
		saver.Request();
		saver.Flush();

		Assert.Equal(5000, saved);
	}

	[Fact]
	public void Saves_RunOneAtATime()
	{
		var running = 0;
		var overlapped = false;
		var saver = new BackgroundSaver(() =>
		{
			if (Interlocked.Increment(ref running) > 1)
			{
				overlapped = true;
			}

			Thread.Sleep(1);
			Interlocked.Decrement(ref running);
		}, exception => throw exception);

		for (var i = 0; i < 50; i++)
		{
			saver.Request();
		}

		saver.Flush();

		Assert.False(overlapped);
	}

	[Fact]
	public void AFailedSave_IsReported_AndLaterOnesStillRun()
	{
		var errors = new List<Exception>();
		var calls = 0;
		var saver = new BackgroundSaver(() =>
		{
			if (Interlocked.Increment(ref calls) == 1)
			{
				throw new IOException("Disk full");
			}
		}, errors.Add);

		saver.Request();
		saver.Flush();
		saver.Request();
		saver.Flush();

		Assert.Single(errors);
		Assert.Equal(2, calls);
	}
}
