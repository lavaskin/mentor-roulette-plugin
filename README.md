# Mentor Roulette Tracker

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for FINAL FANTASY XIV that records each Mentor Roulette run you play and logs it to your account on [Mentor Roulette Tracker](https://mentorroulette.com): the duty, your job, whether it was cleared, whether you joined as a replacement, the wipes, the party's deaths and your time.

It only ever records the Mentor Roulette. Other roulettes, and duties you queue for yourself, are ignored.

## Installing

Add the plugin's repository in Dalamud, install it, and connect it to your account with a token from the site. [The install guide](docs/installing.md) walks through each step.

## Using it

Once it's connected, there's nothing to do: each run is logged when it ends, with a line in chat to say so. `/mrt` shows your runs, and `/mrt config` opens the settings.

- **Don't log the same runs on the site as well,** or each will count twice.
- **Runs played before connecting are kept,** so you can upload them once you connect. If the site can't be reached, the run waits and is tried again later.
- **To fix a run,** edit it on the site.

It never sends your character's name, or anything that identifies anyone else in the duty, so no names or ids: only a count of the party's deaths. When a run counts, how its time is measured, and what the plugin keeps and sends are in [How runs are recorded](docs/runs.md).

## Development

- [Building and testing](docs/development.md), including trying a build in the game
- [Releasing](docs/releasing.md)
