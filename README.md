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

Connecting it to your account (from the site's Account page) is coming in the next version.

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

Only one of the installed plugin and a dev build can be loaded at once.

## Releasing

The version is `<Version>` in `MentorRouletteTracker/MentorRouletteTracker.csproj`. Only lavaskin bumps it, by hand, following [semver](https://semver.org), when merging `dev` into `main`. Pull requests for tickets never change it.

What changed in each version is in `CHANGELOG.md` ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/)), where each ticket's pull request adds its entries under `[Unreleased]`. To release:

1. On `dev`, in one commit: bump the version, rename `[Unreleased]` to `[X.Y.Z] - YYYY-MM-DD`, add a new empty `[Unreleased]` above it, and update the compare links at the bottom (`[Unreleased]` from the new tag to `HEAD`, and the new version from the tag before it to its own; the first release has none before it, so link it to its tag).
2. Merge `dev` into `main`.
3. Tag the merge commit on `main` and push the tag:

   ```bash
   git tag -a vX.Y.Z -m vX.Y.Z <merge commit>
   git push origin vX.Y.Z
   ```

The tag starts the Release workflow (`.github/workflows/release.yml`). It checks the tag matches `<Version>` and is on `main`, builds and tests, and makes a GitHub release with the plugin's zip and the version's changelog. Then it updates `repo.json` on `main`, so Dalamud offers the new version to everyone who has the repository, usually within a few minutes.

If the workflow fails before making the release, fix the problem, then delete the tag (`git push origin :vX.Y.Z` and `git tag -d vX.Y.Z`) and tag again. If it made the release but failed to update `repo.json`, run its last step by hand.

## Icon

`images/icon.png` is the site's logo (a crown on a roulette wheel), the 512px PNG made by the site's own generator (`scripts/logo/` in [mentor-roulette-tracker](https://github.com/lavaskin/mentor-roulette-tracker)). To change it, rebuild it there and copy `public/icon-512.png` over this one.
