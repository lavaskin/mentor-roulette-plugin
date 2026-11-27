# 0002: Reading the game

How Dalamud and the game behave, as far as the plugin relies on it. None of this shows in the code, so code that looks like it could be simpler usually works around one of these. The plugin project (`MentorRouletteTracker`) is the only part that reads the game: it turns what it reads into plain events and values for `Core`, which decides what they mean ([0003](0003-recording-runs.md), [0004](0004-wipes-and-deaths.md)).

## Everything happens on the framework thread

The recorder and the party watcher aren't thread safe, and the game's memory (the duty finder, the party list, the player) is only safe to read on the game's framework thread. So `RunWatcher` runs every Dalamud event handler (`CfPop`, `ZoneInit`, `Logout`, `DutyStarted`, `DutyWiped`, `DutyCompleted`) through `OnFramework`, which uses `IFramework.RunOnFrameworkThread`: straight away if it's already on that thread, and queued if not. A handler that throws is logged ("Couldn't handle a game event"), not rethrown.

This matters most for `IClientState.CfPop`, which Dalamud raises off the framework thread, inside a `Task.Run`. Reading `ContentsFinder` straight from its handler would read the game's memory from another thread.

`MentorRouletteTracker/RunWatcher.cs` (`OnFramework`, `OnCfPop`), `MentorRouletteTracker/PartyReader.cs`.

## The Mentor Roulette is `ContentRoulette` row 9

A queue pop is the Mentor Roulette when the duty finder's popped entry (`ContentsFinder.QueueInfo.PoppedQueueEntry`) is a roulette (`ContentsType.Roulette`) with id 9, the `ContentRoulette` sheet's "Duty Roulette: Mentor". It's the only thing the plugin records (`AGENTS.md`, The one rule). A pop whose `PoppedContentIsInProgress` is set is a duty in progress, so the player is a replacement.

`MentorRouletteTracker/RunWatcher.cs` (`MentorRouletteId`, `OnCfPop`), `MentorRouletteTracker.Core/Models/AcceptedPop.cs`.

## A pop is answered within 45 seconds

The game gives a player 45 seconds to answer a pop, then the party loads in. So an arrival more than a few minutes after a pop wasn't from it: the pop was withdrawn, declined or left to expire. The recorder allows 5 minutes (`RunRecorder.PopLifetime`), which covers the answer and a slow load.

`MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`PopLifetime`).

## The duty finder's queue state says how the player got somewhere

`ContentsFinderQueueState` goes None, Pending, Queued, Ready (at the pop), Accepted (once accepted, and still at the duty's `ZoneInit`), then InContent.

- **Arriving through the duty finder** (`ThroughDutyFinder`) is any state but None, Pending or Queued: the player's going into, or is in, what it popped. After a pop is declined or withdrawn, the queue's back to Queued or None, so a duty entered any other way just after a declined pop isn't taken as the pop's.
- **The accepted pop** (`ReadAcceptedPop`) is read from `QueueInfo` while the state is Accepted: the player's on their way into what it popped. This is how a run starts when the plugin missed the pop itself, because it was reloaded or installed between the pop and arriving ([0003](0003-recording-runs.md#a-pop-the-plugin-missed-is-read-from-the-duty-finder)). It isn't Accepted any more once they're in it, so a zone change inside a duty, or after clearing it, can't start a run.

`MentorRouletteTracker/RunWatcher.cs` (`ThroughDutyFinder`, `ReadAcceptedPop`, `OnZoneInit`).

## `ZoneInit` is the arrival, before the player has loaded in

`IClientState.ZoneInit`, from the zone-init packet, carries the zone's `ContentFinderCondition`. It comes at the start of the loading screen, before the party loads in, so it's the moment the player entered the duty (a run's date played: [0003](0003-recording-runs.md#a-runs-date-played-is-when-the-player-entered-the-duty)), but not when they can be read. `TerritoryChanged` isn't used: it only fires when the territory id changes.

"Loaded in" is the player logged in (`IClientState.IsLoggedIn`), in the world (`IPlayerState.IsLoaded`), and not between areas (`ConditionFlag.BetweenAreas`, `BetweenAreas51`). `RunWatcher` checks it every frame ("settled"), and only then reads the party, the job, and whether the duty has started.

`MentorRouletteTracker/RunWatcher.cs` (`OnZoneInit`, `OnUpdate`).

## Logging in and out don't come in a reliable order

- **`IClientState.Logout` isn't raised on every way out of the game,** and the clock mustn't run while the player's gone. So every frame while the player's in a run's duty, `RunWatcher` also checks `IsLoggedIn`, and tells the recorder they logged out when it's false.
- **On logging in, `ZoneInit` can come before the game counts the player as logged in.** `IClientState.Login` fires on a framework tick once a local player exists, possibly after the login's `ZoneInit`. So the plugin doesn't trust the order of events: once settled after starting (`_lookedOnStart`), and whenever the recorder has a run the player's away from (`IsAway`), it looks where the player is itself, from `GameMain.Instance()->CurrentContentFinderConditionId`, and tells the recorder they've arrived there.

`MentorRouletteTracker/RunWatcher.cs` (`OnUpdate`, `OnLogout`).

## "Duty commenced" doesn't come for everyone

`IDutyState.DutyStarted` is the game's "Duty commenced", which starts the clock so a run's time matches the game's own completion time ([0003](0003-recording-runs.md#the-clock-is-the-players-own-time-in-the-duty)). It doesn't fire for a replacement, who joins after it, or for someone back from a disconnect after it. It can also be missed when the plugin is reloaded as it comes. So `IDutyState.IsDutyStarted`, which Dalamud also sets on the first combat in the duty, is checked every settled frame as a fallback.

`MentorRouletteTracker/RunWatcher.cs` (`OnDutyStarted`, `OnUpdate`).

## The game's wipe message only comes in trials and raids

`IDutyState.DutyWiped` is the director update `0x4000_0005`, the fade-out that resets the whole party. Trials and raids send it; dungeons and guildhests don't, since each player returns to the start themselves. So wipes are also counted from the party, and how the two are matched up is in [0004](0004-wipes-and-deaths.md#the-games-message-and-a-wipe-from-the-party-within-30-seconds-are-the-same-wipe).

`DutyCompleted` fires once per territory, from the flytext or the completion packet.

`MentorRouletteTracker/RunWatcher.cs` (`OnDutyWiped`, `OnDutyCompleted`).

## The party in memory

`PartyReader` reads the party every settled frame while in a run's duty, as plain `PartySlot`s, and leaves deciding which count to `Core` (`PartyReading`, [0004](0004-wipes-and-deaths.md#which-party-slots-count)).

- **Slots:** the main party's (`GroupManager.MainGroup`, up to `MemberCount`), then in an alliance raid (`MainGroup.IsAlliance`) the alliance's other parties' (`AllianceMembers`), only where FFXIVClientStructs' `PartyMember.Flags` has bit `0x01` set (`AllianceSlotInUse`), the game's mark for an alliance slot in use. Then the player, read from themselves.
- **Empty slots** have an `EntityId` of 0 or `0xE0000000` (the game's id for no one). A slot whose `MaxHp` is 0 hasn't loaded.
- **A disconnected member keeps their slot,** with whatever HP they had, and stays in the party list. Only their zone (`TerritoryType`) tells them apart.
- **The player's own slot can lag behind them,** still showing the zone they left just after arriving. So the player is read from their own character (`IObjectTable.LocalPlayer` for their id and HP, `IClientState.TerritoryType`, `ConditionFlag.InCombat`).
- **Whether others are in combat can only be read while they're showing in the game:** it comes from their character in the object table (`IObjectTable.SearchByEntityId`, `StatusFlags.InCombat`), which only has characters close enough to show. Anyone further away reads as out of combat. That's the known miss in [runs.md](../runs.md#wipes).
- **A member is told apart by their `EntityId`,** the game's object id, which is kept in memory only, and never logged, saved or sent (`AGENTS.md`, Privacy).

`MentorRouletteTracker/PartyReader.cs`, `MentorRouletteTracker.Core/Models/PartySlot.cs`, `MentorRouletteTracker.Core/Recording/PartyReading.cs` (`NoEntity`).

## Duties and jobs are the game's row ids

A duty is its `ContentFinderCondition` row id (0 for a zone that isn't a duty), and a job its `ClassJob` row id. Those are what's recorded and sent to the site, which maps them itself; the site never gets names. `ClassJob` row 0 is "adventurer", which isn't a job, so a job of 0 means it hasn't been read yet (the recorder takes the job once it's not 0), and shows as "Unknown job". A job can't change inside a duty, so it's read once.

Names are only for showing runs in the plugin and in its log, from the game's own sheets (`GameNames`, through Lumina), in the game client's language. The game's English names start in lower case ("the Vault", "paladin"), since it puts them mid-sentence, so they're capitalised where they stand alone (`Names.Capitalize`). The plugin project has no implicit usings, and `Lumina.Excel.Sheets` has an `Action` sheet that clashes with `System.Action`, so alias the sheet you need.

`MentorRouletteTracker/GameNames.cs`, `MentorRouletteTracker.Core/Text/Names.cs`, `MentorRouletteTracker.Core/Models/Run.cs` (`GameContentId`, `GameClassJobId`).

## Turning the plugin off isn't the game closing

`RunWatcher.Dispose` runs both when the plugin is turned off, reloaded or updated, and when Dalamud itself unloads (the game closing, or `/xlunload`). `IFramework.IsFrameworkUnloading` is true only in the second case, so it tells them apart. `IDalamudPluginInterface.Reason` can't: turned off, then the game closed, gives `Boot` on the next start. What each means for a run is in [0003](0003-recording-runs.md#the-plugin-turned-off-isnt-a-crash).

`MentorRouletteTracker/RunWatcher.cs` (`Dispose`).

## A dev build shares the installed plugin's config

Dalamud gives a dev plugin (loaded from Dev Plugin Locations, `IDalamudPluginInterface.IsDev`) the same config file and directory as the installed plugin with the same internal name. So a dev build keeps its own files in a `dev` folder ([0005](0005-saving.md#a-dev-build-keeps-its-own-files)).

`MentorRouletteTracker/PluginFiles.cs`.
