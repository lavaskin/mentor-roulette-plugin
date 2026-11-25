# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Runs are dated when you enter the duty rather than when it ends, so a run that goes past midnight counts toward the day you started it (#30).

## [1.2.1] - 2026-11-18

### Changed

- The site has a new address, mentorroulette.com: its links open there, and you stay connected.

## [1.2.0] - 2026-11-17

### Added

- Settings can delete your uploaded runs, or all your runs, from the plugin, without touching the site (#19).

### Changed

- Minor tweaks (#19, #21).

### Fixed

- A run you turned the plugin off during is no longer logged as not cleared, and a chat line says when one isn't logged (#16).

## [1.1.0] - 2026-11-15

### Added

- The chat line says when a run found a new duty or finished a milestone (#14).
- Runs that were waiting to upload get a chat line once they're logged (#14).
- An install guide, from installing the plugin to your first run logged.

### Fixed

- The plugin shows its icon in Dalamud's installer, and no longer offers Dalamud's feedback form (#12).

## [1.0.1] - 2026-11-05

### Changed

- Nothing you'd notice: this release is behind-the-scenes work (#9).

## [1.0.0] - 2026-11-05

### Added

- Install the plugin from its own Dalamud repository, with `/mrt` and `/mrt config` for its windows (#1).
- Records each Mentor Roulette run: the duty, your job, whether it cleared, the wipes and your time (#2).
- Disconnecting and coming back is one run, and a crash counts as not cleared (#2).
- Connect your site account with a token, and each run is logged when it ends (#2).
- `/mrt` lists every run recorded, and uploads any that aren't yet (#2).
- A chat line when a run ends gives its number, duty and time, or why it wasn't logged (#2).
- Runs that couldn't be uploaded are tried again, and ones that weren't can be deleted (#2).

[Unreleased]: https://github.com/lavaskin/mentor-roulette-plugin/compare/v1.2.1...HEAD
[1.2.1]: https://github.com/lavaskin/mentor-roulette-plugin/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/lavaskin/mentor-roulette-plugin/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/lavaskin/mentor-roulette-plugin/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/lavaskin/mentor-roulette-plugin/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/lavaskin/mentor-roulette-plugin/releases/tag/v1.0.0
