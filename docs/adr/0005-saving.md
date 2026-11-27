# 0005: Saving

The plugin's files, and the threads that write them. What a player's told about where their runs are kept is in [runs.md](../runs.md#what-it-keeps-and-sends). The rule running through it: nothing that's been recorded is ever lost to a crash, a power cut or a file that can't be read, and nothing written to disk holds up the game's frame.

## Each file is written whole, then moved over the old one

`JsonFile` writes each of the plugin's own JSON files whole to `<path>.tmp`, flushes it to disk (`Flush(flushToDisk: true)`), then moves it over the old file. A crash mid-write can't leave a half-written file. The flush comes before the move on purpose: without it, the move can reach the disk before the data does, and a power cut would leave an empty file where the old one was.

It's used for `runs.json` and `run-in-progress.json` (through `RunStore`), and a dev build's `dev/settings.json` (through `PluginFiles`). The installed plugin's settings go through Dalamud's own `SavePluginConfig`.

`MentorRouletteTracker.Core/Storage/JsonFile.cs` (`Write`).

## A file that can't be read is set aside, not overwritten

`JsonFile.Read` returns null both when there's no file and when the file isn't valid JSON, and callers start fresh either way. But starting fresh means the next save would overwrite the broken file, and lose every run in it. So a file that can't be read is renamed to `<path>.unreadable-<UTC yyyyMMddHHmmss>`, and the plugin's log says so (the `warn` that `RunStore` and `PluginFiles` pass on), where the runs can be recovered by hand.

Only bad JSON (`JsonException`) is set aside. Any other error reading it (an IO error) throws, since starting with an empty log would overwrite `runs.json`.

`MentorRouletteTracker.Core/Storage/JsonFile.cs` (`Read`).

## The runs, and the run in progress, are two files

`RunStore` keeps them in the plugin's config directory (`pluginConfigs/MentorRouletteTracker/`, or its `dev` folder for a dev build):

- **`runs.json`:** every run the plugin keeps, uploaded or not, until the player deletes it, as `{ "version": 1, "runs": [...] }`.
- **`run-in-progress.json`:** the run being played. It exists only while there is one: saving null deletes it, and on starting, no file means no run to carry on.

`MentorRouletteTracker.Core/Storage/RunStore.cs`.

## Files saved by older versions still read

Fields added since the first version are optional, so the files older versions wrote still read, and each one's null says the older version didn't have it:

- **`Run.Deaths`** is null in a run recorded before the plugin counted deaths (and in a run with more than the site takes: [0003](0003-recording-runs.md#wipes-and-deaths-are-the-partys-while-the-players-in-the-duty)).
- **`InProgressRun.Deaths`** is null in a run in progress saved by a version that didn't count them, which carries on without them.
- **`InProgressRun.EnteredAt`** is null in a run in progress saved by a version that didn't keep it, which is dated at its end minus its time.
- **`InProgressRun.AwaitingDutyCommenced`, `PluginTurnedOff` and `ClockRanWhileOff`** are false when missing.

So these aren't `required`, unlike the other fields: making one `required` would stop the older files reading.

`MentorRouletteTracker.Core/Models/Run.cs`, `MentorRouletteTracker.Core/Models/InProgressRun.cs`.

## The run in progress is saved so a crash loses at most a minute

`RunWatcher` saves the run in progress (`RunRecorder.Snapshot`) on every change (arriving, the clock starting, the job, a death or wipe, logging out) and every minute while the player's in a run's duty (`SaveEvery`), so a crash loses at most a minute of its time. `InProgressRun` holds what the recorder needs to carry the run on ([0003](0003-recording-runs.md#a-crash-carries-the-run-on-or-ends-it-as-not-cleared)): its time so far (`ElapsedSeconds`, up to `LastSeenAt`), when the player entered and was last seen in the duty (both UTC), and the job (0 until read).

`MentorRouletteTracker/RunWatcher.cs` (`Save`, `OnUpdate`), `MentorRouletteTracker.Core/Models/InProgressRun.cs`.

## A finished run is in `runs.json` before the run in progress is cleared

When a run ends, `RunWatcher.Finished` hands it on first (`runEnded`: `Tracker` keeps it in the run log, which asks for `runs.json` to be saved), and only then saves the run in progress as none. That save waits for the runs file (`runsSaved`, which is `Tracker.RunsSaved`, blocking until the last save of the runs is done) before deleting `run-in-progress.json`. If the runs couldn't be saved, it keeps the old in-progress file, so the next start carries the run on or ends it again, rather than lose it.

`MentorRouletteTracker/RunWatcher.cs` (`Finished`, the in-progress saver), `MentorRouletteTracker/Tracker.cs` (`RunsSaved`).

## Saves run off the game's frame, one at a time

`BackgroundSaver` runs each save on the thread pool, so file writes never hold up the game's frame (`AGENTS.md`). Saves run one at a time, and requests made while one is running fold into one more save, not one each, so the last request is always followed by a save that sees everything up to it. A save reads what to write when it runs, not when it's asked for, so callers pass something that reads the current state (`RunLog.Runs`, the latest snapshot), never a copy taken at the request.

`Flush` waits until no save is running or waiting, and returns whether the last save worked (not every save: an earlier failure followed by a success is true). It's used on the way out, and by the run in progress's save above.

`MentorRouletteTracker.Core/Storage/BackgroundSaver.cs`.

## The run log is read every frame while uploads change it

`RunLog` holds every run, newest first by date played. The windows read it every frame on the framework thread while uploads change it on the thread pool, so it's thread safe: changes take a lock and publish a new list, and reads take no lock (`Volatile`). `Runs` is a snapshot that never changes once read, so a window can go through it across a frame without locking.

- **`Changed`** is raised after every change, outside the lock, on whichever thread made it, usually an upload's. So a handler must be thread safe: `Tracker`'s is the runs saver's `Request`.
- **`NotUploaded`** is the runs waiting to upload, in the order they upload: oldest first. Failed runs aren't in it, so they're set aside until the player retries them ([0006](0006-uploading-and-the-sites-api.md#a-400-sets-the-run-aside)).
- **`Update`** does nothing when the run's gone, since an upload finishing can race with the player deleting the run.
- **`Delete`** refuses an uploaded run: it's on the site, where it's fixed. Deleting all runs, or all uploaded ones, can remove uploaded runs, and never touches the site.

`MentorRouletteTracker.Core/Storage/RunLog.cs`.

## The settings are one connection for every character

`Configuration` is the plugin's settings, which Dalamud keeps in its plugin config file. One connection covers every character on the install: the token isn't per character, and the character's name is never stored. `Token` is the plugin token from the site's Account page, null when not connected, and `Username` is whose account it's for, as the site said when it connected (it isn't refreshed). Core reads them through `IUploadSettings`, so it needs no Dalamud reference. Settings change on the framework thread only, and are saved through a `BackgroundSaver` like the runs.

`MentorRouletteTracker/Configuration.cs`, `MentorRouletteTracker.Core/Uploading/IUploadSettings.cs`, `MentorRouletteTracker/Tracker.cs` (`SaveConfig`, `ConnectAsync`).

## A dev build keeps its own files

Dalamud gives a dev build the same config file and directory as the installed plugin ([0002](0002-reading-the-game.md#a-dev-build-shares-the-installed-plugins-config)), and both are tried on one install. So a dev build (`IsDev`) keeps its settings in the config directory's `dev` folder as `settings.json` (through `JsonFile`, not Dalamud's config), beside its own `runs.json` and `run-in-progress.json`. Trying a build then never touches the installed plugin's runs, and each keeps its own server and token. A separate internal name for dev builds would have let both load at once, with two `/mrt`s and every run recorded twice.

`MentorRouletteTracker/PluginFiles.cs`.
