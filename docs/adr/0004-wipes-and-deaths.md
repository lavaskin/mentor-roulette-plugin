# 0004: Wipes and deaths

How `PartyWatcher` counts the party's deaths and says when it wipes, from what each frame shows of the party and the game's wipe message. The player-facing version is in [runs.md](../runs.md#wipes); this is every rule, for anyone changing it. The recorder decides when the watcher is listening ([0003](0003-recording-runs.md#wipes-and-deaths-are-the-partys-while-the-players-in-the-duty)), and how the party is read from the game is in [0002](0002-reading-the-game.md#the-party-in-memory).

## The party is watched because the game's message isn't enough

The game only sends its wipe message in trials and raids ([0002](0002-reading-the-game.md#the-games-wipe-message-only-comes-in-trials-and-raids)), so in dungeons and guildhests the party's HP and combat, read every frame, are all there is. Both are used, and the same wipe seen both ways counts once (below). The party watcher has no Dalamud types and is unit tested as timelines, one frame per change (`PartyWatcherTests`). It isn't thread safe.

`MentorRouletteTracker.Core/Recording/PartyWatcher.cs`.

## A fight is anyone alive in combat

A fight starts on the first frame where any member who's alive is in combat, and is over on the first frame where none is. Someone who died and returned to the start is alive and out of combat (outside the boss's wall), so they neither keep a fight going nor end it while the rest of the party fights.

`MentorRouletteTracker.Core/Recording/PartyWatcher.cs` (`Seen`, `EndFight`).

## A death is a member seen going from alive to dead

- **A death** is a member the watcher has already seen going from alive (HP above 0) to dead (HP 0) between frames, in any party of an alliance. Dying, being raised and dying again is two. A death outside a fight still counts. Tank abilities that keep HP at 1 or more never count, since only 0 is dead.
- **Someone first seen dead isn't a death** until they're up and die again: after the plugin starts, after the player comes back (or the watcher is reset), or when they've just joined. The watcher can't know whether that death was already counted, or happened in this run.
- **Members are told apart by id** (the game's `EntityId`), not slot or order. So a replacement in a slot is someone new, seen for the first time: they don't inherit the old member's state, and a dead old member being replaced by a live one isn't a raise.
- **The same member twice in one frame** (the same id in two slots) is seen once, so they die once and count once.

`MentorRouletteTracker.Core/Recording/PartyWatcher.cs` (`Seen`, `Member`), `MentorRouletteTracker.Core/Models/PartyFrame.cs` (`Deaths`).

## A wipe is everyone out of the fight, checked as a death is seen

- **A wipe** is everyone in the party (all of the alliance in an alliance raid, 24) out of the fight: each is dead, or died this fight and is now out of combat (returned to the start). Someone raised and back in combat is still fighting, so three dying while the fourth finishes the fight isn't a wipe. Alone, the player's death is a wipe.
- **It's checked only on a frame where a death is seen,** so it counts as the last one dies. A fight that ends with everyone up again isn't one, lying dead for many frames counts it once, and everyone first seen dead (after the plugin starts, or coming back) isn't one, since that can't count a wipe that already happened.
- **The wipe check runs before the fight-over check** in the same frame, so a frame where the last member dies and nobody's left in combat still sees the fight's deaths and counts the wipe.
- **A member who's left, or isn't here, doesn't have to die.** Whoever isn't in a frame is forgotten (`ForgetTheGone`): they left the party, or `PartyReading` left them out (not loaded, or not in the player's zone, which is how a disconnected member reads). Coming back, they're seen for the first time again: back still dead isn't a second death, and back alive after the fight they died in starts afresh. A member who joins mid-fight has to die too.

`MentorRouletteTracker.Core/Recording/PartyWatcher.cs` (`Seen`, `ForgetTheGone`), `MentorRouletteTracker.Core/Models/PartyFrame.cs` (`Wipe`, `WipeMembers`).

## Between fights, only those still dead carry into the next one

Ending a fight doesn't forget its deaths straight away: on each frame between fights, once that frame's members have been seen (`ForgetDeathsOfTheLiving`, before the check for a new fight), anyone still dead counts as having died in the next fight (`DiedThisFight`), and everyone alive starts it afresh. So the whole party lying dead at once is always a wipe, however many fights it took (three die and nobody raises them, then the last one pulls the next pack alone and dies), and deaths in a fight the party won don't count toward the next once those members are up. A member whose slot loads in dead mid-fight starts with `DiedThisFight` set too, so if they return to the start, they count toward a wipe as having died this fight; only the wipe's trigger, a death seen, can't come from first sightings.

`MentorRouletteTracker.Core/Recording/PartyWatcher.cs` (`ForgetDeathsOfTheLiving`, `Member.DiedThisFight`).

## The game's message and a wipe from the party within 30 seconds are the same wipe

- **Whichever comes first counts,** and the other is skipped if it comes within `SameWipe` (30 seconds, inclusive), noting `WipeSkipped` with the gap. The message can come before the last death shows in the party (the fade-out comes first), and a wipe seen from the party usually comes before the message. 31 seconds apart, they're two wipes.
- **Each wipe absorbs only one from the other side:** a wipe from the party, then the message (skipped), then another message, is two wipes.
- **The message on its own is a wipe,** with no deaths at all, since the duty can fail some other way (an escort dying).
- **The message ends the fight,** since the game raises everyone at the start, out of combat. Its deaths are forgotten from the next frame, so a member the game raised who waits while the others start straight away and die isn't part of a wipe.

`MentorRouletteTracker.Core/Recording/PartyWatcher.cs` (`SameWipe`, `GameWiped`, `Seen`), `MentorRouletteTracker.Core/Models/WipeSource.cs`.

## Resetting forgets the party and the fight

`Reset` forgets every member, the fight, both wipes' times and the last party size noted, so everyone's seen for the first time again next frame. The recorder calls it when a run starts and when the player's back in it after being away ([0003](0003-recording-runs.md#wipes-and-deaths-are-the-partys-while-the-players-in-the-duty)).

`MentorRouletteTracker.Core/Recording/PartyWatcher.cs` (`Reset`).

## Which party slots count

`PartyReading.Read` turns the game's party and alliance slots, and the player, into the `Party` the watcher sees (`PartyReader` reads them from the game: [0002](0002-reading-the-game.md#the-party-in-memory)).

- **A slot counts if it's loaded** (max HP above 0) **and in the player's zone.** Anyone else is left out (and counted as `NotHere`, for the log), so they don't have to die for a wipe. A disconnected member keeps their slot, so without the zone check they'd hold up every wipe for the rest of the run.
- **Empty slots** are skipped, and aren't counted as not here.
- **The player is read from themselves** rather than their slot, which can lag behind them ([0002](0002-reading-the-game.md#the-party-in-memory)), so on their own the party is just them. A player who hasn't loaded (max HP 0, or no id) is read from their slot instead, if they have one, and otherwise isn't in it.
- **The same id in two slots** is one member, the first kept.
- **A dead member is never in combat,** whatever the game says.
- **Others' combat is only known while they're showing in the game** ([0002](0002-reading-the-game.md#the-party-in-memory)), so if the player has died and returned to the start while the rest of the party fights out of sight, the fight looks over, its deaths are forgotten, and the others dying later isn't a wipe. That's a known miss, left alone: players are told about it in [runs.md](../runs.md#wipes), and they can fix the count on the site. `PartyWatcherTests.TheKnownMiss_BackAtTheStart_WithTheFightTooFarAwayToShow` pins it, so a fix changes that test on purpose.

`MentorRouletteTracker.Core/Recording/PartyReading.cs`, `MentorRouletteTracker.Core/Models/Party.cs`, `MentorRouletteTracker.Core/Models/PartySlot.cs`, `MentorRouletteTracker.Core/Models/PartyMember.cs`.

## When reading the party fails, the run is still recorded

If reading the party throws, `RunWatcher` logs it once a run, at Warning, and carries on with the rest of the frame (the job, the save). The run's still recorded, with its wipes from the game's wipe message alone, and no deaths counted while it fails.

`MentorRouletteTracker/RunWatcher.cs` (`PartySeen`).
