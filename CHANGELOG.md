# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- The plugin installs from its own repository in Dalamud, and `/mrt` and `/mrt config` open its runs and settings windows (#1).
- Records each Mentor Roulette run you play: the duty, your job, whether it was cleared, whether you were a replacement, the wipes and your time in the duty (from "Duty commenced", like the game's completion time), and nothing else (#2).
- Disconnecting and coming back to the same duty is one run, and a run the game crashed out of is recorded as not cleared (#2).
- Connect to your Mentor Roulette Tracker account with a token from the site's Account page, and each run is logged there when it ends (#2).
- `/mrt` lists every run recorded, with whether it's uploaded, a link to it on the site, and "Upload N runs" for any that aren't (#2).
- A gold line in chat when a run ends gives its run number, duty and time, or why it wasn't logged. Clicking it opens your runs, and it can be turned off (#2).
- A run the site can't take right now is tried again later, a run it refuses can be retried, and a run that isn't uploaded can be deleted (#2).

[Unreleased]: https://github.com/lavaskin/mentor-roulette-plugin/commits/dev
