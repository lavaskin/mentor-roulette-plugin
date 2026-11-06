#!/usr/bin/env bash
# Prints repo.json for one release: the manifest DalamudPackager wrote (icon and feedback included, from the
# .csproj), plus where Dalamud downloads the plugin from. Dalamud reads repo.json from main (see the README),
# so this only goes there once the release's zip is up.
#
#   repo-json.sh <manifest> <tag> <changelog file> <repository>
#   e.g. repo-json.sh MentorRouletteTracker/bin/x64/Release/MentorRouletteTracker/MentorRouletteTracker.json \
#          v1.0.0 notes.md lavaskin/mentor-roulette-plugin
set -euo pipefail

manifest=${1:?usage: repo-json.sh <manifest> <tag> <changelog file> <repository>}
tag=${2:?missing the tag}
notes=${3:?missing the changelog file}
repository=${4:?missing the repository}

zip="https://github.com/$repository/releases/download/$tag/MentorRouletteTracker.zip"

jq --arg zip "$zip" --rawfile changelog "$notes" --arg updated "$(date +%s)" '[. + {
	DownloadLinkInstall: $zip,
	DownloadLinkUpdate: $zip,
	DownloadLinkTesting: $zip,
	Changelog: ($changelog | rtrimstr("\n")),
	LastUpdate: $updated,
	IsHide: false,
	IsTestingExclusive: false
}]' "$manifest"
