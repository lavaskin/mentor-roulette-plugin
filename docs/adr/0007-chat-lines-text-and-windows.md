# 0007: Chat lines, text and windows

What the plugin says, in chat, in its windows and in its log, and how. The rule running through it: the plugin writes things the way the site does, so a run reads the same in both, and its words are the site's (`AGENTS.md`, Words).

## The chat line when a run ends reports, and names the run by duty and time

"[MRT] Logged #1234, The Vault, 32m 10s." The player just played the run, so only the duty and time say which run it was (job, cleared and wipes are obvious to them), and the rest is what happened to it: logged as #N, or why it wasn't. It reports, rather than telling the player what to do. A logged run then says how its time compares with the usual, then anything it found, in that order: "… 32m 10s. Slow, 4m over the usual. Every dungeon seen!"

- **The pace** comes from the site's answer to the upload ([0006](0006-uploading-and-the-sites-api.md#what-the-site-said-about-a-run-is-optional)), so it's only there when the site gave one. Its own setting (`ChatLinePace`) can leave it out; it's read when the outcome arrives.
- **The gap** is in seconds under a minute ("12s"), and from a minute in whole minutes, rounded half away from zero ("4m", "1h 5m"), since the usual is the median of a handful of runs and isn't exact to the second (`Pace.Gap`).

`MentorRouletteTracker.Core/Text/ChatLine.cs` (`Format`), `MentorRouletteTracker.Core/Text/Pace.cs`.

## The chat line for runs that were waiting is one line a pass

When any upload logs runs that were waiting (on start, catching up before a new run, "Upload N runs", Retry), one line says so: "[MRT] Logged 3 runs that were waiting.", plus what they found if any found a new duty. A run that just ended isn't one of them, since it has its own line ([0006](0006-uploading-and-the-sites-api.md#a-run-that-just-ended-is-told-about-once-whoever-uploads-it)). The runs must be in the order they were logged, which the milestones rely on (below).

`MentorRouletteTracker.Core/Text/ChatLine.cs` (`FormatWaiting`).

## The chat line for a run that wasn't logged says what to do

"[MRT] The Vault wasn't logged, since the plugin was off for part of it. You can log it on the site." ([0003](0003-recording-runs.md#the-plugin-turned-off-isnt-a-crash)). Unlike the others, it says what the player can do, since nothing else tells them the run's missing. It doesn't open the runs window when clicked, since the run isn't there.

`MentorRouletteTracker.Core/Text/ChatLine.cs` (`FormatEndMissed`), `MentorRouletteTracker/Tracker.cs` (`EndMissed`).

## What a run found is the site's toast, and only the biggest

What logging a new duty found is in the site's words for its toast: "New duty found!", or the milestone a run finished: "Every duty in the roulette seen!", "Every dungeon seen!" or "Every Heavensward dungeon seen!". Only the biggest is given, since it includes the smaller ones.

- **The scopes are ordered smallest first** (`Milestones.Scope`: type in expansion, type, roulette), and the biggest is the last after sorting, so a new scope goes in at its size.
- **A duty with no type** can only finish the whole roulette.
- **The expansion's name** is the part of the site's label after ": " ("3.0: Heavensward" is "Heavensward"), as the site's toast writes it. A label without one is used whole. The type is lower-cased.
- **Several runs together** give the biggest milestone any of them finished, else how many new duties they found ("New duty found!", "3 new duties found!"), else nothing. Of two milestones as big, the later one's is used, since its counts are the newest, so the runs must be in the order they were logged.

`MentorRouletteTracker.Core/Text/Milestones.cs`.

## Chat lines are gold, and clickable

Every chat line is in the game's `UIColor` row 31 (`ChatColour`): a soft gold, #F3CC64, and a darker gold on light themes. Clicking one opens the runs window, or brings it to the front (a `DalamudLinkPayload` from `IChatGui.AddChatLinkHandler`), unless it's a line that shouldn't. The `[MRT]` tag is part of the line (`ChatLine`). One setting (`Configuration.ChatLine`) covers every chat line, checked as each prints.

`MentorRouletteTracker/Tracker.cs` (`ChatColour`, `ChatMessage`).

## Chat lines are said in order, on the framework thread

Chat lines are said on any thread (an upload's, mostly), and printed on the framework thread. They go through one queue (`_toSay`), drained in order on the framework thread (`PrintSaid`): each line's framework call prints whatever's queued rather than its own line, so they print in the order they were said. That matters because the waiting runs' line is said before the outcome of a run that ended in the same upload ([0006](0006-uploading-and-the-sites-api.md#a-run-that-just-ended-is-told-about-once-whoever-uploads-it)), so it comes first.

The waiting runs' line is held until the player's logged in (`_afterLogin`, touched on the framework thread only, printed on `IClientState.Login`), since the plugin starts at the title screen and uploads straight away, and Dalamud's `ChatGui.Print` doesn't wait for login: a line printed then would be lost.

`MentorRouletteTracker/Tracker.cs` (`Say`, `PrintSaid`, `OnLogin`, `OnWaitingRunsLogged`).

## Times, dates and names are written like the site's

- **Times** are the site's (its `formatDuration`): "1h 30m", "55m 24s", "45m", "30s". Zero parts are left out, and from an hour so are the seconds. Zero is "0s".
- **Dates** are how the site writes a day and its time, "Nov 5, 2026, 3:12 AM", in en-US whatever the PC's culture, shown in the PC's time zone from the run's UTC date.
- **Duty and job names** are the game's, capitalised where they stand alone ([0002](0002-reading-the-game.md#duties-and-jobs-are-the-games-row-ids)).

`MentorRouletteTracker.Core/Text/Duration.cs`, `MentorRouletteTracker.Core/Text/DateText.cs`, `MentorRouletteTracker.Core/Text/Names.cs`.

## The log says every step of a run, and no one by name

The recorder and the party watcher note everything they see and decide as `RunNote`s, and `RunWatcher` writes each as a line in Dalamud's log through `LogLine`: duties and jobs by name and id ("The Heroes' Gauntlet (737)"), times like the site's, and party members as counts only. No note says who anyone is: not the player, and not the ids that tell party members apart (`AGENTS.md`, Privacy).

- **The level:** a note with no run to apply to (`RunNote.Ignored`: a pop for anything but the Mentor Roulette, the game's wipe message with no run or while the player's away from it) is logged at Debug, and everything during a run at Information, so a run's whole story is in the log at Dalamud's default level ([development.md](../development.md)).
- **Party counts:** the player's own party for a death or raise in it, and the whole alliance for one in another party. The party's size is noted only when it changes, with who isn't here (not loaded, or not in the zone).
- **Every note has its own line.** `LogLine.Format` ends in the record's own text, which names its type, so a note without a case would log that. `LogLineTests.EveryKindOfNote_HasALineOfItsOwn` finds every kind of note by reflection and fails on one without a line.

`MentorRouletteTracker.Core/Models/RunNote.cs`, `MentorRouletteTracker.Core/Text/LogLine.cs`, `MentorRouletteTracker/RunWatcher.cs` (`OnNoted`).

## A delete is confirmed by a popup each window draws itself

`ConfirmDelete` asks to confirm a delete, with Delete and Cancel. An ImGui popup belongs to the window that opens it (it's found by its id in the current id stack), so each window has its own, and draws it every frame from its `Draw`, outside any row's id: opening one inside a table row's `PushID` and drawing it elsewhere wouldn't match. Its title needs an `##id` unique in its window. What it says is read every frame, so counts stay current, and none (null) closes it: there's nothing left to delete, because the runs were deleted or uploaded meanwhile.

`MentorRouletteTracker/Windows/ConfirmDelete.cs`.
