# 0008: CI and releasing

The CI, Release and Link ticket workflows, and `repo.json`. How to release, and the deploy key, are in [releasing.md](../releasing.md); what CI runs is in `AGENTS.md`. The repo is public, so every workflow runs on GitHub's runners, which cost nothing for a public repo.

## CI runs on pull requests and on pushes to `main`

CI runs on every pull request into `main` or `dev`, on pushes to `main`, to catch anything pushed straight to the branch releases are made from, and by hand (`workflow_dispatch`). A new push to a pull request cancels its older run; runs on `main` all finish.

`.github/workflows/ci.yml` (`on`, `concurrency`).

## CI builds against Dalamud's latest release

Dalamud's assemblies aren't on NuGet, so CI downloads Dalamud's latest release and points `DALAMUD_HOME` at it, which is where `Dalamud.NET.Sdk` looks for them. It isn't pinned: when Dalamud moves to a new API level, CI fails until the plugin moves to it, which Dalamud requires before it will load the plugin anyway.

Restore runs with `--locked-mode`, since `Dalamud.NET.Sdk` turns lock files on and the plugin's `packages.lock.json` must match its packages.

`.github/workflows/ci.yml` (`DALAMUD_HOME`, Download Dalamud, Restore).

## CI reports every failure in one run

The Release build runs even when the tests fail (`!cancelled()`, as long as Restore worked), so a run reports both. It's the same build a release makes, and its zip is attached to the run as the `MentorRouletteTracker` artifact, so a pull request's build can be tried in the game before it's released ([development.md](../development.md#trying-a-build-in-the-game)). The comment check ([0001](0001-no-code-comments.md)) runs whatever failed before it.

`.github/workflows/ci.yml` (Build, Upload the plugin, Check comments).

## A release goes one run at a time, and `repo.json` last

- **One run at a time** (`concurrency: release`, `cancel-in-progress: false`), so two pushes can't both release a version or both commit `repo.json`. The run started by the release's own `repo.json` commit waits for it, then finds the version released and `repo.json` pointing at it, so does nothing.
- **Whether to release comes from `release-plan`,** shared with the other FFXIV repos in `lavaskin/ffxiv-workflows`, before anything is built. The three outcomes (`release`, `repo-json` and `none`) are in [releasing.md](../releasing.md). `repo.json` points at a release when a `DownloadLinkInstall` holds `/releases/download/<tag>/`.
- **Finishing a release whose `repo.json` push failed** needs no build: the release's zip holds the manifest, and its body is the notes.
- **`repo.json` is updated last,** after the release and its zip are up, so Dalamud never offers a version it can't download.
- **The push to `main` uses a deploy key** (`ssh-key` on `actions/checkout`): why, and setting it up, are in [releasing.md](../releasing.md#the-deploy-key).

`.github/workflows/release.yml`.

## `repo.json` is the packaged manifest plus where to download it

`.github/scripts/repo-json.sh <manifest> <tag> <changelog file> <repository>` prints `repo.json` for one release: the manifest DalamudPackager wrote (`MentorRouletteTracker/bin/x64/Release/MentorRouletteTracker/MentorRouletteTracker.json`), which already has the icon and the feedback setting from the `.csproj`, plus the release zip's address for install, update and testing, the version's changelog, the time, and `IsHide` and `IsTestingExclusive` set to false.

`.github/scripts/repo-json.sh`.

## The icon and feedback setting are in the `.csproj`, not only `repo.json`

An installed plugin's icon and feedback button come from the manifest in its own folder, which DalamudPackager writes from the `.csproj` (`IconUrl`, `AcceptsFeedback`), not from `repo.json`. Feedback is off, since the installer's feedback form goes to Dalamud's team rather than to a custom repository's author.

A dev plugin doesn't use `IconUrl`: Dalamud shows `images/icon.png` from beside its DLL, so the `.csproj` copies `images/icon.png` into the build output.

`MentorRouletteTracker/MentorRouletteTracker.csproj` (`IconUrl`, `AcceptsFeedback`, the `images\icon.png` item).

## Link ticket links each pull request into `dev` to its ticket

It calls the Link ticket workflow in `lavaskin/ffxiv-workflows` at `@main`, which links a pull request to its ticket in `lavaskin/ffxiv-hub` from the branch name (`feature/<n>-<desc>`). The triggers it needs (`synchronize` and `edited` as well as `opened` and `reopened`), and why, are in [that repo's README](https://github.com/lavaskin/ffxiv-workflows#link-ticket).

`.github/workflows/link-ticket.yml`.
