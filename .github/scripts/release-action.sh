#!/usr/bin/env bash
# Prints what the Release workflow does for a version pushed to main:
#   release     vX.Y.Z isn't tagged yet: build it and make its release
#   repo-json   it's released, but repo.json doesn't point at it yet (a release whose repo.json push failed)
#   none        it's released and repo.json points at it (a merge that didn't bump the version)
# Fails if the version isn't X.Y.Z, or the remote can't be asked for its tags.
#
#   release-action.sh <version> [repo.json] [remote]     e.g. release-action.sh 1.0.0
set -euo pipefail

version=${1:?usage: release-action.sh <version> [repo.json] [remote]}
repo_json=${2:-repo.json}
remote=${3:-origin}
tag=v$version

if [[ ! $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
	echo "The plugin's version, $version, isn't X.Y.Z: fix <Version> in MentorRouletteTracker.csproj" >&2
	exit 1
fi

# --exit-code: 2 when no tag matches, anything else nonzero when the remote couldn't be asked
status=0
git ls-remote --exit-code --tags "$remote" "refs/tags/$tag" >/dev/null || status=$?
if ((status == 2)); then
	echo release
	exit 0
elif ((status != 0)); then
	echo "Couldn't read $remote's tags (git ls-remote exited $status)" >&2
	exit 1
fi

if jq -e --arg path "/releases/download/$tag/" 'any(.[]; .DownloadLinkInstall | contains($path))' "$repo_json" >/dev/null; then
	echo none
else
	echo repo-json
fi
