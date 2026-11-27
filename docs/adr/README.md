# Architecture decision records

Why the code is the way it is, wherever the obvious way would be wrong: how Dalamud and the game behave and the plugin relies on, when a run counts, threads and files, the contract with the site's API, and privacy. The code has no comments ([0001](0001-no-code-comments.md)), so a decision like that is written here, in the ADR for its topic, before anyone "fixes" the code. What a player sees is in [runs.md](../runs.md); building and trying a build in [development.md](../development.md); releasing in [releasing.md](../releasing.md). An ADR adds only what those pages don't say.

| ADR | Covers |
|---|---|
| [0001](0001-no-code-comments.md) | No code comments, the comment check, and what takes a comment's place |
| [0002](0002-reading-the-game.md) | How Dalamud and the game behave, as the plugin relies on it: threads, events and their order, the duty finder, the party in memory, and the game's data |
| [0003](0003-recording-runs.md) | The recorder: pops, arrivals, when a run starts, ends and counts, the clock, logging out, crashes, the plugin turned off, and the date played |
| [0004](0004-wipes-and-deaths.md) | The party watcher: fights, deaths and wipes, and which party slots count |
| [0005](0005-saving.md) | The runs and settings on disk, the run in progress, dev builds' files, saving off the game's frame, and files saved by older versions |
| [0006](0006-uploading-and-the-sites-api.md) | Upload order and rules, the site's endpoints, errors and retries, and the server address |
| [0007](0007-chat-lines-text-and-windows.md) | The chat lines, how times, dates and names are written, the log lines, and the windows |
| [0008](0008-ci-and-releasing.md) | The CI, Release and Link ticket workflows, and `repo.json` |

## Writing one

- **Add to the ADR for the topic** when there is one: a new section, headed with the decision in a short sentence. Start a new numbered ADR (`NNNN-topic.md`, the next number) only for a topic none covers, and add it to the table above.
- **A section says the decision and why,** in plain sentences that stand alone without the code beside them, then the files and symbols it's about: `MentorRouletteTracker.Core/Recording/RunRecorder.cs` (`Arrived`).
- **Only what's not obvious from the code,** and nothing another page in `docs/` already says: link it instead.
- **How things are now,** not their history. When a decision changes, rewrite its section; when its code goes, delete it.
- **For someone new to the project:** the repo is public, so an ADR names no one, and explains Dalamud and the game as far as the decision needs.
