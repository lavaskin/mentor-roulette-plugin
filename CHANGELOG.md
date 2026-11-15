# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- The chat line says when a run found a new duty, or finished a milestone like every Heavensward dungeon (#14).
- Runs that were waiting to upload get a chat line when they're logged, like "Logged 3 runs that were waiting.", with any new duty or milestone they found (#14).

### Fixed

- The installed plugin shows its icon in Dalamud's plugin installer, and no longer offers Dalamud's feedback form, which went to Dalamud's team rather than to the plugin's author (#12).

## [1.0.1] - 2026-11-05

### Changed

- A dev build of the plugin keeps its own settings and runs, apart from the installed plugin's, so trying one no longer changes the installed plugin's runs or disconnects it (#9).

## [1.0.0] - 2026-11-05

### Added

- The plugin installs from its own repository in Dalamud, and `/mrt` and `/mrt config` open its runs and settings windows (#1).
- Records each Mentor Roulette run you play: the duty, your job, whether it was cleared, whether you were a replacement, the wipes and your time in the duty (from "Duty commenced", like the game's completion time), and nothing else (#2).
- Disconnecting and coming back to the same duty is one run, and a run the game crashed out of is recorded as not cleared (#2).
- Connect to your Mentor Roulette Tracker account with a token from the site's Account page, and each run is logged there when it ends (#2).
- `/mrt` lists every run recorded, with whether it's uploaded, a link to it on the site, and "Upload N runs" for any that aren't (#2).
- A gold line in chat when a run ends gives its run number, duty and time, or why it wasn't logged. Clicking it opens your runs, and it can be turned off (#2).
- A run the site can't take right now is tried again later, a run it refuses can be retried, and a run that isn't uploaded can be deleted (#2).

[Unreleased]: https://github.com/lavaskin/mentor-roulette-plugin/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/lavaskin/mentor-roulette-plugin/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/lavaskin/mentor-roulette-plugin/releases/tag/v1.0.0
