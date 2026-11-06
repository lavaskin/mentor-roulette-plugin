# AGENTS.md - mentor-roulette-plugin

A Dalamud plugin (FINAL FANTASY XIV) that records each Mentor Roulette run and logs it to the user's account on Mentor Roulette Tracker (<https://mentor.lavask.in>). Its companions are the site (`lavaskin/mentor-roulette-tracker`) and its API (`lavaskin/ffxiv_api`), which this talks to.

## The one rule

**The Mentor Roulette only.** The plugin records nothing else, ever: not other roulettes, and not mentor-eligible duties queued directly or reached any other way. Tracking anything else defeats the point of the site. A run is the Mentor Roulette when the queue pop is a roulette (`ContentsType.Roulette`) with `ContentRoulette` id 9.

## Layout

| Project | What |
| --- | --- |
| `MentorRouletteTracker` | The plugin, on `Dalamud.NET.Sdk` (Dalamud API 15). Only turns Dalamud's events into plain events for `Core`, and draws the windows. |
| `MentorRouletteTracker.Core` | Everything that doesn't need the game. **No Dalamud, Lumina or FFXIVClientStructs references**, so it can be unit tested. Logic goes here whenever it can. |
| `MentorRouletteTracker.Core.Tests` | xUnit tests for `Core` |

- **`Core`'s folders,** each its own namespace (`Core.Models`, and so on):
  - `Models`: plain records and enums (`Run`, `InProgressRun`, `RunOutcome`, `AcceptedPop`), one type per file, with its enum beside it.
  - `Recording`: `RunRecorder`.
  - `Storage`: `RunLog` (in memory), `RunStore` (on disk), `JsonFile` (how each file's written), `BackgroundSaver`.
  - `Uploading`: `RunKeeper`, `Uploader`, `IUploadSettings`.
  - `Api`: talking to the site.
  - `Text`: how things are written (`Duration`, `DateText`, `ChatLine`, `Names`).
- **How it fits together:**
  - `RunWatcher` (plugin) feeds the game's events (queue pop, zone arrival, loaded in, duty commenced, wipe, duty complete, logout) to `Core.Recording.RunRecorder`, a state machine that gives out finished runs, and saves the run in progress on every change and every minute.
  - `Tracker` (plugin) hands each finished run to `Core.Uploading.RunKeeper`, which keeps it in `RunLog` (saved by `RunStore`) and uploads it through `Uploader` and `Api.MentorApiClient` if auto-upload's on.
  - `PluginFiles` (plugin) says where the settings and runs live. A dev build keeps its own in the config directory's `dev` folder, apart from the installed plugin's.
  - The upload rules (oldest first, what holds up later runs, 401 and 400) are in `Uploader`'s summary.
- **Style:** tabs in C#, file-scoped namespaces, `_camelCase` private instance fields and PascalCase static ones, like the API.
- **Windows** use Dalamud's `Window` and `WindowSystem`. **Commands:** `/mrt` (runs) and `/mrt config` (settings).
- **Nothing blocks the game's frame:** network calls and file writes run off the framework thread.
- **Privacy:** never store or send the character's name, or anything about other players. Send the site only what a run needs.

## Words

The same words as the site, for the same things:

- a **run**, which is **cleared** or not
- a **replacement** (joined a duty in progress)
- **wipes**
- times written like the site's: "55m 24s", "1h 30m" (`Core.Text.Duration.Format`)
- Sentence case everywhere.

## Commands

| Task | Command |
| --- | --- |
| Build | `dotnet build` (`-c Release` for what releases ship) |
| Tests | `dotnet test` |

The build needs Dalamud's assemblies. `Dalamud.NET.Sdk` finds the ones XIVLauncher installs (`%AppData%\XIVLauncher\addon\Hooks\dev` on Windows, `~/.xlcore/dalamud/Hooks/dev` with XIVLauncher.Core on Linux), or set `DALAMUD_HOME` to use others. Trying a build in the game is in the README (Building), and only the person testing it in the game can do that.

After any change, build in Release and run the tests. **Never run `git clean -x`** (or `-d` without `-n` first): it deletes untracked files, including new work that isn't committed yet. To get a clean build, delete `bin/` and `obj/` instead.

## CI

`.github/workflows/ci.yml` runs on every pull request into `main` or `dev`, and every push to `main`. It downloads Dalamud's latest release into `DALAMUD_HOME`, restores with `--locked-mode`, runs the tests, builds in Release, and attaches the plugin's zip as an artifact.

- **The plugin project's `packages.lock.json`** (Dalamud.NET.Sdk turns lock files on) must match its packages, or restore fails. Commit it when packages change.
- **When Dalamud moves to a new API level,** the build breaks until the plugin moves to it (`Dalamud.NET.Sdk`'s version in the plugin's `.csproj`, plus whatever API changes it brings). Dalamud won't load a plugin built for an old API level anyway, so fix it by updating, not by pinning Dalamud.

`.github/workflows/link-ticket.yml` links each pull request to its ticket from the branch name. The same file is in `ffxiv_api` and `mentor-roulette-tracker`: keep all three identical.

## Releasing

In the README (Releasing). Releasing is merging: the Release workflow runs on every push to `main`, and releases `<Version>` (tagging it `vX.Y.Z` and updating `repo.json` on `main`) if it isn't released yet. Its three scripts are in `.github/scripts/`, so they can be run locally: `release-action.sh` decides what a push does, `changelog-section.sh` reads the release's notes, and `repo-json.sh` writes `repo.json`.

## Changelog

`CHANGELOG.md` follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/). Each ticket's PR adds its entries under `[Unreleased]`, in the same PR: what a player using the plugin would notice, one line each in plain words, ending with the ticket's number (`(#2)`). Tech debt with no visible change adds nothing.

## Rules

- **Never change `<Version>`** in `MentorRouletteTracker.csproj`: it's bumped by hand when releasing (README, Releasing).
- **Custom repository only.** The plugin is never submitted to Dalamud's official repository (DalamudPluginsD17), so its rules on review and AI use don't apply. Dalamud's plugin restrictions and its rules for talking to a backend server still do (<https://dalamud.dev/plugin-publishing/restrictions>, <https://dalamud.dev/plugin-development/technical-considerations>).
- **Tickets and branches:** tickets are GitHub issues, tracked on the FFXIV project board, and branches are `feature/mrp-<n>-<short-desc>` off `dev`, where `<n>` is the issue's number.
