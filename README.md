# Mentor Roulette Tracker (Dalamud plugin)

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for FINAL FANTASY XIV that records each Mentor Roulette run you play and logs it to your account on [Mentor Roulette Tracker](https://mentor.lavask.in).

For each run it records:
- the duty
- your job
- whether it was cleared
- whether you joined as a replacement
- how many times the party wiped
- your time in the duty

It only ever records the Mentor Roulette. Other roulettes, and duties you queue for yourself, are ignored.

## Installing

The plugin is in its own repository, not Dalamud's official one.

1. In the game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this URL into the empty box, click **+**, and then **Save and Close**:

   ```
   https://raw.githubusercontent.com/lavaskin/mentor-roulette-plugin/main/repo.json
   ```

3. Type `/xlplugins`, search for **Mentor Roulette Tracker**, and install it.

`/mrt` opens your runs, and `/mrt config` the settings.

## Using it

1. On the site, make a token on your **Account** page (the Plugin panel).
2. In the game, open `/mrt config`, paste the token and click **Connect**. It says who you're connected as. One connection covers every character on the install.
3. Play Mentor Roulettes. When each run ends, the plugin logs it to your account and says so in chat ("[MRT] Logged #1234, The Vault, 32m 10s."), adding "New duty found!" for your first run on a duty, or the milestone it finished ("Every Heavensward dungeon seen!"). Runs that were waiting get a line of their own when they're logged ("[MRT] Logged 3 runs that were waiting."). Click a line to open your runs.

Without connecting, it still records every run and keeps it, so you can connect and upload them later.

- **Your runs** (`/mrt`): every run the plugin has recorded, newest first, kept forever whether it's uploaded or not. An uploaded run's number links to it on the site.
- **Upload N runs** sends every run that isn't uploaded, oldest first. Runs are numbered in the order they reach the site, so runs uploaded late come after any runs you've logged since, but keep the dates they were played.
- **Auto-upload** (on by default) uploads each run when it ends. With it on, don't log the same runs on the site as well: each would count twice.
- **If the site can't be reached,** the run waits, and it tries again when the plugin starts and after your next run. **If the site refuses a run,** it's marked Failed with the reason, and you can retry it. A retry can't log a run twice.
- **Fixing a run:** upload it, then edit it on the site. A run that isn't uploaded can be deleted.

### When a run counts

- A pop you decline, withdraw from or let expire records nothing.
- Leaving or being kicked before the clear is a run that wasn't cleared.
- Disconnecting and coming back to the same duty is one run, and the time you were away doesn't count.
- If the game crashes and you don't get back in, the run is recorded as not cleared the next time you log in.

Your time is your own time in the duty, to the duty-complete message, or to leaving if it wasn't cleared. It starts at "Duty commenced", so a cleared run's time matches the game's completion time. As a replacement, it starts once you've loaded in. Loading screens, and time disconnected, don't count.

### What it keeps and sends

The runs are kept in the plugin's config folder (`pluginConfigs/MentorRouletteTracker/runs.json`), and the token in its settings. It sends the site only the duty, your job, whether it was cleared, whether you were a replacement, the wipes, your time and when the run ended. Never your character's name, or anything about anyone else in the duty.

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and the game installed with XIVLauncher, run with Dalamud at least once. `Dalamud.NET.Sdk` builds against the Dalamud that XIVLauncher installed:
- **Windows:** `%AppData%\XIVLauncher\addon\Hooks\dev`
- **Linux (XIVLauncher.Core):** `~/.xlcore/dalamud/Hooks/dev`

Set `DALAMUD_HOME` to use a Dalamud from somewhere else.

```bash
dotnet build                # Debug
dotnet build -c Release     # what releases ship
dotnet test                 # the tests, which don't need the game
```

The plugin is built to `MentorRouletteTracker/bin/x64/<Debug|Release>/MentorRouletteTracker.dll`.

### Trying a build in the game

1. In `/xlsettings` → **Experimental** → **Dev Plugin Locations**, add the full path to `MentorRouletteTracker.dll` above and save.
   - On Linux, the game runs under Wine, so it's a Wine path: `Z:` followed by the path, with backslashes, e.g. `Z:\home\you\code\mentor-roulette-plugin\MentorRouletteTracker\bin\x64\Debug\MentorRouletteTracker.dll`.
2. In `/xlplugins` → **Dev Tools** → **Installed Dev Plugins**, enable it.
3. After rebuilding, disable and enable it again to load the new build.

A pull request's build can be tried the same way: CI attaches it to each run as the `MentorRouletteTracker` artifact. Unzip it, and point the dev plugin location at its `MentorRouletteTracker.dll`.

Only one of the installed plugin and a dev build can be loaded at once. A dev build (a CI artifact too) keeps its own settings and runs, in `pluginConfigs/MentorRouletteTracker/dev/` (`settings.json`, `runs.json`), so trying one never touches the installed plugin's runs, and each stays connected to its own site. To switch, disable one and enable the other.

To try it against a dev copy of the site, change the server address in `/mrt config` → **Advanced** (disconnect first). Advanced only shows when the plugin's loaded as a dev plugin: the installed plugin always uses https://mentor.lavask.in. The game under Wine reaches `localhost` on the same PC. The plugin logs what it sees (each queue pop, each arrival and whether it came through the duty finder, and each run) to Dalamud's log, `/xllog`, at the Debug level.

## Releasing

The version is `<Version>` in `MentorRouletteTracker/MentorRouletteTracker.csproj`. It's bumped by hand, following [semver](https://semver.org), as part of releasing (below). Pull requests for tickets never change it.

What changed in each version is in `CHANGELOG.md` ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/)), where each ticket's pull request adds its entries under `[Unreleased]`. To release:

1. On `dev`, in one commit: bump the version, rename `[Unreleased]` to `[X.Y.Z] - YYYY-MM-DD`, add a new empty `[Unreleased]` above it, and update the compare links at the bottom (`[Unreleased]` from the new tag to `HEAD`, and the new version from the tag before it to its own; the first release has none before it, so link it to its tag).
2. Merge `dev` into `main`.

That's all: there's no tag to push. Every push to `main` runs the Release workflow (`.github/workflows/release.yml`), which reads `<Version>`:
- **Not released yet:** it checks the changelog has the version's section, builds and tests, and makes a GitHub release with the plugin's zip and the version's changelog, which tags the merge `vX.Y.Z`. Then it updates `repo.json` on `main`, so Dalamud offers the new version to everyone who has the repository, usually within a few minutes.
- **Already released** (a merge that didn't bump the version, or the workflow's own `repo.json` commit): nothing.
- **Released, but `repo.json` doesn't point at it** (the last release's `repo.json` push failed): it writes `repo.json` from that release, without building.

Pushing a tag by hand doesn't release anything.

It pushes `repo.json` with a deploy key, since `main`'s ruleset blocks the workflow's own token and GitHub Actions can't be added to the bypass list of a personal repository. Set up once:

1. Make a key with no passphrase: `ssh-keygen -t ed25519 -N '' -C mentor-roulette-plugin-release -f release_key`.
2. **Settings → Deploy keys → Add deploy key:** paste `release_key.pub`, and tick **Allow write access**.
3. **Settings → Secrets and variables → Actions → New repository secret:** `RELEASE_DEPLOY_KEY`, with all of `release_key` (the private key) as its value. Then delete both files.
4. **Settings → Rules → Rulesets → main → Bypass list → Add bypass:** **Deploy keys**, set to Always allow.

If the workflow fails before making the release (a missing changelog section, a failing test), nothing is released: fix it on `dev` and merge again. If it made the release but failed to update `repo.json`, re-run the workflow (or push to `main` again), and it finishes the release.
