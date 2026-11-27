# 0003: Recording runs

How `RunRecorder` turns what happens in the game into finished runs. The player-facing version of these rules is [runs.md](../runs.md); this is why the recorder does what it does, for anyone changing it. How the game's events are read is in [0002](0002-reading-the-game.md), and the wipes and deaths in [0004](0004-wipes-and-deaths.md).

## The recorder is a state machine of plain events, on one thread

`RunRecorder` takes the game's events as plain calls with their times (`Popped`, `Arrived`, `LoadedIn`, `Commenced`, `JobKnown`, `PartySeen`, `GameWiped`, `Completed`, `LoggedOut`). `Arrived` and `Completed` return the run they finished, if any, and `LoadedIn`, `Commenced` and `PartySeen` return whether anything changed, so the plugin knows to save. It has no Dalamud types, so every rule here is unit tested (`RunRecorderTests`). It isn't thread safe: the plugin calls it from the framework thread only ([0002](0002-reading-the-game.md#everything-happens-on-the-framework-thread)).

Everything it sees and decides comes out of `Noted` as a `RunNote`, for the log ([0007](0007-chat-lines-text-and-windows.md#the-log-says-every-step-of-a-run-and-no-one-by-name)).

`MentorRouletteTracker.Core/Recording/RunRecorder.cs`.

## A run starts on arriving in a duty from a Mentor Roulette pop

- **A pop is remembered until the next arrival,** which uses it up. Only a Mentor Roulette pop is remembered, and any other pop replaces it, so a Mentor Roulette declined, then a duty queued directly and popped, records nothing (`Popped`).
- **The arrival starts a run** if there's a pop, the zone is a duty (its `ContentFinderCondition` isn't 0), the duty finder put the player there (`throughDutyFinder`), and the pop is no older than `PopLifetime`. Why each check is there is in 0002: [the duty finder](0002-reading-the-game.md#the-duty-finders-queue-state-says-how-the-player-got-somewhere) and [the pop's age](0002-reading-the-game.md#a-pop-is-answered-within-45-seconds).
- **A replacement** is a pop for a duty in progress. Their clock starts on loading in, rather than at "Duty commenced", which they never see.

`MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`Popped`, `Arrived`, `PopLifetime`).

## A pop the plugin missed is read from the duty finder

The plugin forgets a pop if it's reloaded (or installed) between the pop and arriving. So on arriving in a zone, `RunWatcher` also reads the pop the duty finder holds as accepted ([0002](0002-reading-the-game.md#the-duty-finders-queue-state-says-how-the-player-got-somewhere)) and passes it to `Arrived`, which uses it when it has no pop of its own, timed as the arrival.

`MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`Arrived`'s `acceptedPop`), `MentorRouletteTracker.Core/Models/AcceptedPop.cs`.

## A run ends at the clear, or on arriving anywhere else

The duty-complete message (`Completed`) finishes the run as cleared, and its time ends there: loot and lingering afterwards don't count, and leaving after it finishes nothing more. Arriving anywhere other than the run's duty finishes it as not cleared. Arriving in the run's own duty again (a zone change inside it, or coming back) carries it on.

`MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`Completed`, `Arrived`, `Finish`).

## The clock is the player's own time in the duty

- **It starts at "Duty commenced"** (`Commenced`), like the game's own completion time, so the loading screen and the wait for the party to load in don't count: a run the game times at 27s is 27s here too. The game saying the duty has started is the fallback for a missed "Duty commenced" (`dutyHadStarted`, [0002](0002-reading-the-game.md#duty-commenced-doesnt-come-for-everyone)).
- **A replacement's clock starts once they've loaded in** (`LoadedIn`), and so does the clock of someone coming back after logging out or disconnecting, since "Duty commenced" happened while they were away (or before they joined).
- **It stops while the player's logged out or disconnected** (`LoggedOut`), and starts again once they've loaded back into the duty. Loading screens never count.
- **A run's time is whole seconds, at least 1** (`Run.DurationSeconds`).

`MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`Commenced`, `LoadedIn`, `LoggedOut`, `StopClock`), `MentorRouletteTracker.Core/Models/Run.cs` (`DurationSeconds`).

## A run's date played is when the player entered the duty

It's the arrival that started the run, at the start of the loading screen in, in UTC (`Run.DatePlayed`). So a run that goes past midnight counts for the day it started, on the site's streaks, calendar and runs by hour. A disconnect and return keeps the first entry. It's not the pop (which has no time after a reload between the pop and the duty), and not "Duty commenced" (which never comes for a run left before it, and comes again after a disconnect).

Runs recorded by older versions of the plugin were dated when they ended, and keep those dates. A run in progress saved by a version that didn't keep when the player entered (`InProgressRun.EnteredAt` is null) is dated at its end minus its time. For a run that ends while the player's away, its end is when they were last seen in it.

`MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`Current.EnteredAt`, `Finish`), `MentorRouletteTracker.Core/Models/Run.cs` (`DatePlayed`), `MentorRouletteTracker.Core/Models/InProgressRun.cs` (`EnteredAt`).

## A run the player never loaded into gives out nothing

A run the player disconnected or crashed out of on the loading screen in, and didn't come back to, has no job (the game's job is only read once they've loaded in) and no time. The site needs the job, so the run is dropped (`DropReason.NeverLoadedIn`) rather than sent and refused forever.

`MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`Finish`), `MentorRouletteTracker.Core/Models/RunNote.cs` (`DropReason`).

## A crash carries the run on, or ends it as not cleared

The run in progress is saved on every change, and every minute while the player's in the duty ([0005](0005-saving.md#the-run-in-progress-is-saved-so-a-crash-loses-at-most-a-minute)). When the plugin starts with one saved, `Restore` carries it on, treating the player as logged out since it was last seen (`LastSeenAt`), so the time in between doesn't count. Arriving back in its duty carries it on once they've loaded in; arriving anywhere else ends it as not cleared, at `LastSeenAt` rather than the arrival. The game closing (and `/xlunload`) counts as a crash.

A run saved before "Duty commenced" (the plugin was reloaded while the party loaded in) still waits for it once it's back (`AwaitingDutyCommenced`), so the wait doesn't count, as it wouldn't have without the reload.

`MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`Restore`, `Snapshot`, `IsAway`), `MentorRouletteTracker.Core/Models/InProgressRun.cs`.

## The plugin turned off isn't a crash

When the plugin is turned off, reloaded or updated during a run, rather than the game closing ([0002](0002-reading-the-game.md#turning-the-plugin-off-isnt-the-game-closing)), it may miss how the run ends, and it could have been cleared while the plugin was off. So `TurnedOff` saves the run marked `PluginTurnedOff`, and the mark stays until the run ends, across later crashes. A run with the mark is given out only if the recorder sees the clear itself. Ending any other way drops it (`DropReason.PluginWasOff`) and raises `EndMissed` with its duty, for the chat line that says it wasn't logged ([0007](0007-chat-lines-text-and-windows.md#the-chat-line-for-a-run-that-wasnt-logged-says-what-to-do)). Its time and wipes would be partial, so any version of it on the site would be wrong, and the site has no "unknown".

If the clock was running when it was turned off (the player was in the duty, not away), `ClockRanWhileOff` is saved too. Back in the duty, the time it was off counts, since the player was there all along: the clock carries on from `LastSeenAt`. Wipes and deaths while it was off are lost. Turned off while logged out, the time off doesn't count. Turned off, then logged out and back into the same duty, counts the logged-out time, since the plugin didn't see the logout: that's known, and left alone.

`MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`TurnedOff`, `Finish`, `EndMissed`, `Current.PluginWasOff`, `Current.ClockRanWhileOff`), `MentorRouletteTracker.Core/Models/InProgressRun.cs` (`PluginTurnedOff`, `ClockRanWhileOff`).

## Wipes and deaths are the party's, while the player's in the duty

The recorder hands each frame of the party and the game's wipe message to `PartyWatcher` ([0004](0004-wipes-and-deaths.md)) only while the player's in the run's duty, and resets it (`PartyWatcher.Reset`) at a run's start and on coming back from being away, so the party's seen afresh each time. A zone change inside the duty doesn't reset it, so a fight carries on across one. So logging out forgets the fight, a new run starts with a new party, and a restore while the party lay dead doesn't count the wipe again.

Deaths are counted the same way. A run restored from a version that didn't count them (`InProgressRun.Deaths` is null) carries on without them, rather than send part of the count. More than the site takes (`Run.MaxDeaths`, 999) are left out of the run, which is then sent without them.

`MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`PartySeen`, `GameWiped`, `CountDeaths`, `Finish`), `MentorRouletteTracker.Core/Models/Run.cs` (`MaxDeaths`, `Deaths`).
