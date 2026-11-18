# Building and testing

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

## Trying a build in the game

1. In `/xlsettings` → **Experimental** → **Dev Plugin Locations**, add the full path to `MentorRouletteTracker.dll` above and save.
   - On Linux, the game runs under Wine, so it's a Wine path: `Z:` followed by the path, with backslashes, e.g. `Z:\home\you\code\mentor-roulette-plugin\MentorRouletteTracker\bin\x64\Debug\MentorRouletteTracker.dll`.
2. In `/xlplugins` → **Dev Tools** → **Installed Dev Plugins**, enable it.
3. After rebuilding, disable and enable it again to load the new build.

A pull request's build can be tried the same way: CI attaches it to each run as the `MentorRouletteTracker` artifact. Unzip it, and point the dev plugin location at its `MentorRouletteTracker.dll`.

Only one of the installed plugin and a dev build can be loaded at once. A dev build (a CI artifact too) keeps its own settings and runs, in `pluginConfigs/MentorRouletteTracker/dev/` (`settings.json`, `runs.json`), so trying one never touches the installed plugin's runs, and each stays connected to its own site. To switch, disable one and enable the other.

To try it against a dev copy of the site, change the server address in `/mrt config` → **Advanced** (disconnect first). Advanced only shows when the plugin's loaded as a dev plugin: the installed plugin always uses https://mentor.lavask.in. The game under Wine reaches `localhost` on the same PC.

The plugin logs what it sees (each queue pop, each arrival and whether it came through the duty finder, and each run) to Dalamud's log, `/xllog`, at the Debug level.

Releasing is in [releasing.md](releasing.md).
