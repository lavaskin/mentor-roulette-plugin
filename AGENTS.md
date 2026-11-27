# AGENTS.md - mentor-roulette-plugin

A Dalamud plugin (FINAL FANTASY XIV) that records each Mentor Roulette run and logs it to the user's account on Mentor Roulette Tracker (<https://mentorroulette.com>). Its companions are the site (`lavaskin/mentor-roulette-tracker`) and its API (`lavaskin/ffxiv_api`), which this talks to.

## The one rule

**The Mentor Roulette only.** The plugin records nothing else, ever: not other roulettes, and not mentor-eligible duties queued directly or reached any other way. Tracking anything else defeats the point of the site. A run is the Mentor Roulette when the queue pop is a roulette (`ContentsType.Roulette`) with `ContentRoulette` id 9.

## Layout

| Project | What |
| --- | --- |
| `MentorRouletteTracker` | The plugin, on `Dalamud.NET.Sdk` (Dalamud API 15). Only turns Dalamud's events into plain events for `Core`, and draws the windows. |
| `MentorRouletteTracker.Core` | Everything that doesn't need the game. **No Dalamud, Lumina or FFXIVClientStructs references**, so it can be unit tested. Logic goes here whenever it can. |
| `MentorRouletteTracker.Core.Tests` | xUnit tests for `Core` |

- **`Core`'s folders,** each its own namespace (`Core.Models`, and so on):
  - `Models`: plain records and enums (`Run`, `InProgressRun`, `RunOutcome`, `AcceptedPop`, `Party`, `RunNote`), one type per file, with its enum beside it.
  - `Recording`: `RunRecorder`, `PartyWatcher` (wipes and deaths), `PartyReading` (which party slots count).
  - `Storage`: `RunLog` (in memory), `RunStore` (on disk), `JsonFile` (how each file's written), `BackgroundSaver`.
  - `Uploading`: `RunKeeper`, `Uploader`, `IUploadSettings`.
  - `Api`: talking to the site.
  - `Text`: how things are written (`Duration`, `DateText`, `ChatLine`, `LogLine`, `Milestones`, `Names`).
- **How it fits together:**
  - `RunWatcher` (plugin) feeds the game's events (queue pop, zone arrival, loaded in, duty commenced, the game's wipe message, duty complete, logout) to `Core.Recording.RunRecorder`, a state machine that gives out finished runs, and saves the run in progress on every change and every minute. While the player's in a run's duty, it also reads the party every frame through `PartyReader` (HP and combat, by the game's object id) and hands it over as plain `PartySlot`s.
  - `RunRecorder` counts wipes and deaths through `PartyWatcher`, which turns each frame's party and the game's wipe message into deaths and wipes (its summary has the rules). Everything it sees and decides comes out as `RunNote`s, which `RunWatcher` logs through `Core.Text.LogLine`. Notes carry counts, never who anyone is.
  - `Tracker` (plugin) hands each finished run to `Core.Uploading.RunKeeper`, which keeps it in `RunLog` (saved by `RunStore`) and uploads it through `Uploader` and `Api.MentorApiClient` if auto-upload's on.
  - `PluginFiles` (plugin) says where the settings and runs live. A dev build keeps its own in the config directory's `dev` folder, apart from the installed plugin's.
  - The upload rules (oldest first, what holds up later runs, 401 and 400) are in `Uploader`'s summary.
- **Style:** tabs in C#, file-scoped namespaces, `_camelCase` private instance fields and PascalCase static ones, like the API.
- **Windows** use Dalamud's `Window` and `WindowSystem`. **Commands:** `/mrt` (runs) and `/mrt config` (settings).
- **Nothing blocks the game's frame:** network calls and file writes run off the framework thread.
- **Privacy:** never store or send the character's name, or anything that identifies another player: no names or ids, only a count of the party's deaths. The ids `PartyWatcher` tells members apart by stay in memory, and are never logged, written or sent. Send the site only what a run needs.

## Words

The same words as the site, for the same things:

- a **run**, which is **cleared** or not
- a **replacement** (joined a duty in progress)
- **wipes**, and **deaths** (the whole party's, the player included)
- times written like the site's: "55m 24s", "1h 30m" (`Core.Text.Duration.Format`)
- Sentence case everywhere.

## Commands

| Task | Command |
| --- | --- |
| Build | `dotnet build` (`-c Release` for what releases ship) |
| Tests | `dotnet test` |

The build needs Dalamud's assemblies. `Dalamud.NET.Sdk` finds the ones XIVLauncher installs (`%AppData%\XIVLauncher\addon\Hooks\dev` on Windows, `~/.xlcore/dalamud/Hooks/dev` with XIVLauncher.Core on Linux), or set `DALAMUD_HOME` to use others. Trying a build in the game is in `docs/development.md`, and only the person testing it in the game can do that.

After any change, build in Release and run the tests. **Never run `git clean -x`** (or `-d` without `-n` first): it deletes untracked files, including new work that isn't committed yet. To get a clean build, delete `bin/` and `obj/` instead.

## CI

`.github/workflows/ci.yml` runs on every pull request into `main` or `dev`, and every push to `main`. It downloads Dalamud's latest release into `DALAMUD_HOME`, restores with `--locked-mode`, runs the tests, builds in Release, and attaches the plugin's zip as an artifact.

- **The plugin project's `packages.lock.json`** (Dalamud.NET.Sdk turns lock files on) must match its packages, or restore fails. Commit it when packages change.
- **When Dalamud moves to a new API level,** the build breaks until the plugin moves to it (`Dalamud.NET.Sdk`'s version in the plugin's `.csproj`, plus whatever API changes it brings). Dalamud won't load a plugin built for an old API level anyway, so fix it by updating, not by pinning Dalamud.

`.github/workflows/link-ticket.yml` links each pull request to its ticket from the branch name. It calls the Link ticket workflow in `lavaskin/ffxiv-workflows`, which every FFXIV repo shares, at `@main`: change it there, not here.

## Releasing

In `docs/releasing.md`. Releasing is merging: the Release workflow runs on every push to `main`, and releases `<Version>` (tagging it `vX.Y.Z` and updating `repo.json` on `main`) if it isn't released yet. Whether a version is tagged yet, and its notes from the changelog, come from `release-plan` in `lavaskin/ffxiv-workflows`, which every FFXIV repo's Release workflow shares. The rest is this repo's own: the build, and `.github/scripts/repo-json.sh`, which writes `repo.json` and can be run locally.

## Changelog

**Players read this changelog.** A release's section is its notes on GitHub and its changelog in Dalamud's plugin installer, so every entry is written for someone who installed the plugin and has never seen its code.

`CHANGELOG.md` follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/). Each ticket's PR adds its entries under `[Unreleased]`, in the same PR. Tickets live in `lavaskin/ffxiv-hub`, which is private, so an entry cites none of them: a player can't open the link. Only an issue a player opened in this repo gets its number (`(#2)`). Each entry:

- **Is what a player would see or notice in the game,** from the outside looking in: a new window, option, command or chat line, something that works differently, or something that was broken and isn't.
- **Has no technical details at all:** no files, folders, settings files, classes, APIs, endpoints, status codes, builds, workflows or how it works. Say what the player sees ("Runs that failed to upload are tried again"), never why or how ("retries on a 503").
- **Is one short sentence in plain words.** The ticket and the PR have the details.

A change no player would notice (tech debt, dev builds, CI, the release process, docs for contributors) adds nothing. If a release has nothing a player would notice, its section says so in one line rather than listing what changed underneath.

## Rules

- **The changelog is for players, never technical** (see Changelog): Dalamud's installer shows it to everyone who installs the plugin.
- **Never change `<Version>`** in `MentorRouletteTracker.csproj`: it's bumped by hand when releasing (`docs/releasing.md`).
- **Custom repository only.** The plugin is never submitted to Dalamud's official repository (DalamudPluginsD17), so its rules on review and AI use don't apply. Dalamud's plugin restrictions and its rules for talking to a backend server still do (<https://dalamud.dev/plugin-publishing/restrictions>, <https://dalamud.dev/plugin-development/technical-considerations>).
- **Tickets and branches:** every ticket is an issue in `lavaskin/ffxiv-hub`, on the FFXIV project board, and branches are `feature/<n>-<short-desc>` off `dev`, where `<n>` is the ticket's number there. Its `CONTRIBUTING.md` has the rules every FFXIV repo shares (tickets, branches, pull requests, changelogs, releases); this file has what's particular to the plugin, like its changelog. A player's issue opened here stays open here, and the hub's ticket for the work links to it.
