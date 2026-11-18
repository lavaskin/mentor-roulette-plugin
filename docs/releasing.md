# Releasing

The version is `<Version>` in `MentorRouletteTracker/MentorRouletteTracker.csproj`. It's bumped by hand, following [semver](https://semver.org), as part of releasing (below). Pull requests for tickets never change it.

What changed in each version is in `CHANGELOG.md` ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/)), where each ticket's pull request adds its entries under `[Unreleased]`. To release:

1. On `dev`, in one commit: bump the version, rename `[Unreleased]` to `[X.Y.Z] - YYYY-MM-DD`, add a new empty `[Unreleased]` above it, and update the compare links at the bottom (`[Unreleased]` from the new tag to `HEAD`, and the new version from the tag before it to its own; the first release has none before it, so link it to its tag).
2. Merge `dev` into `main`.

That's all: there's no tag to push. Every push to `main` runs the Release workflow (`.github/workflows/release.yml`), which reads `<Version>`:
- **Not released yet:** it checks the changelog has the version's section, builds and tests, and makes a GitHub release with the plugin's zip and the version's changelog, which tags the merge `vX.Y.Z`. Then it updates `repo.json` on `main`, so Dalamud offers the new version to everyone who has the repository, usually within a few minutes.
- **Already released** (a merge that didn't bump the version, or the workflow's own `repo.json` commit): nothing.
- **Released, but `repo.json` doesn't point at it** (the last release's `repo.json` push failed): it writes `repo.json` from that release, without building.

Pushing a tag by hand doesn't release anything.

Players add the repository as `https://mentorroulette.com/plugin/repo.json`: the site's server proxies that address to `repo.json` on `main`, so a release shows there as soon as the workflow pushes it. Moving or renaming `repo.json` on `main` needs the site's server config changed with it, or that address breaks.

If the workflow fails before making the release (a missing changelog section, a failing test), nothing is released: fix it on `dev` and merge again. If it made the release but failed to update `repo.json`, re-run the workflow (or push to `main` again), and it finishes the release.

## The deploy key

The workflow pushes `repo.json` with a deploy key, since `main`'s ruleset blocks the workflow's own token and GitHub Actions can't be added to the bypass list of a personal repository. Set up once:

1. Make a key with no passphrase: `ssh-keygen -t ed25519 -N '' -C mentor-roulette-plugin-release -f release_key`.
2. **Settings → Deploy keys → Add deploy key:** paste `release_key.pub`, and tick **Allow write access**.
3. **Settings → Secrets and variables → Actions → New repository secret:** `RELEASE_DEPLOY_KEY`, with all of `release_key` (the private key) as its value. Then delete both files.
4. **Settings → Rules → Rulesets → main → Bypass list → Add bypass:** **Deploy keys**, set to Always allow.
