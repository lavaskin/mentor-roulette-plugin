# 0006: Uploading and the site's API

How runs get to the site, and the contract with the site's API. What a player sees is in [runs.md](../runs.md#uploading). The site's API is its own repo; the plugin's side of the contract is `IMentorApi`, and anything the plugin reads from it has to keep working against an older site and a newer one, since players update the plugin when they like.

## Uploads go oldest first, always

The site numbers runs in the order they arrive, and can't reorder them, so `Uploader` uploads the runs that aren't uploaded oldest first (by date played), always. There's no per-run Upload button, only "Upload N runs", and auto-upload sends any older runs that are waiting before the one that just ended.

- **The site can't be reached, or can't answer:** the run stays waiting, and holds up the runs after it, so they still go in order. `Problem` says why until a run goes.
- **A 401:** uploading stops, and `NeedsReconnect` says to reconnect, which pauses auto-upload until the player connects again, or a later upload gets an answer (logged or refused). Nothing is marked failed, since the runs aren't at fault.
- **A 400:** see below.

A retry is always safe: each run has a plugin run id (`Run.Id`, made when it's recorded), and the site returns a run it already has with that id instead of logging it twice. So a run is retried until the site answers.

`MentorRouletteTracker.Core/Uploading/Uploader.cs`, `MentorRouletteTracker.Core/Storage/RunLog.cs` (`NotUploaded`).

## A 400 sets the run aside

A 400 is the site refusing the run, and saying why. Sending it again won't change that, so the run is marked Failed with the site's message (`Run.FailedReason`) and set aside: it's left out of the runs waiting (`RunLog.NotUploaded`), so the runs after it carry on. It waits for the player to retry it (`RetryAsync`), which puts it back in line in its place by date, after any older runs waiting, so a retry doesn't jump the queue.

`MentorRouletteTracker.Core/Uploading/Uploader.cs` (`RetryAsync`), `MentorRouletteTracker.Core/Models/Run.cs` (`RunStatus.Failed`, `FailedReason`).

## One pass at a time, and deletes wait for it

`Uploader` runs one pass at a time (a semaphore, `_gate`):

- **A pass uploads every run waiting until one has to wait,** and runs that end while it's going go in the same pass. Asking for a pass while one is running waits for it, then makes its own.
- **Each run goes with the connection as it is then,** read again before every run, so disconnecting stops the pass.
- **Deletes wait for no pass to be running** (`DeleteAsync`, `DeleteUploadedAsync`, `DeleteAllAsync`), so a run in the middle of uploading can't end up logged on the site and gone here, and a run uploaded by the pass is deleted with the other uploaded runs. Deleting a run returns whether it was: not if it was uploaded meanwhile.
- **`Logged` is raised at the end of each pass that logged any runs,** with them in the order they were logged, before the next pass starts. It's raised in a `finally`, so a pass that stops partway, or throws, still reports what it logged. It's raised on the thread that uploaded them, while the pass still holds the uploader, so a handler mustn't wait on an upload: it would deadlock.
- **Connecting or disconnecting** (`ConnectionChanged`) clears `NeedsReconnect` and `Problem`, since what went wrong before no longer applies.

`MentorRouletteTracker.Core/Uploading/Uploader.cs`.

## A run that just ended is told about once, whoever uploads it

`RunKeeper` keeps each run that ends in the run log before returning, so it's never lost if the upload fails, then uploads it off the calling thread if auto-upload's on and the plugin's connected. The run that just ended gets its own chat line, with what its upload found, even when another pass (like "Upload N runs") is the one that uploads it. Every other run logged is one that was waiting, told about together in `WaitingRunsLogged` ([0007](0007-chat-lines-text-and-windows.md#chat-lines-are-said-in-order-on-the-framework-thread)).

- **`_ending`** holds the runs that just ended whose outcome isn't decided yet, with what the site said once a pass logs one. It's locked, since passes run on other threads.
- **With auto-upload off,** the keeper still waits for a pass already running (`Uploader.WaitForPassAsync`), since it sends the runs that end meanwhile.
- **Logged comes first,** even with auto-upload off: if a pass logged the run while it was in `_ending`, its outcome is Logged, whatever the settings say. Once it's taken out of `_ending`, a pass that logs it later tells it as a run that was waiting instead, so no run is told about twice.
- **`WaitingRunsLogged` comes on the thread that uploaded them,** before the outcome of a run that ended in the same pass, and while the pass holds the uploader, so its handler mustn't wait on an upload either.
- **When the plugin starts** (`ResumeAsync`), it uploads any runs left waiting, if auto-upload's on.

`MentorRouletteTracker.Core/Uploading/RunKeeper.cs`, `MentorRouletteTracker.Core/Models/RunOutcome.cs`.

## The site's two plugin endpoints

The plugin uses only the site's two plugin endpoints, which take a plugin token as `Authorization: Bearer <token>`:

- **`GET /api/plugin/me`** → `{ "username" }`: whose account the token is for. Connecting checks the token with it, and keeps the token only if the site accepts it.
- **`POST /api/plugin/runs`** logs a run, and returns `{ "mentorRouletteLogId", "runNumber" }` and what it found. Sending one that's already logged (by its `pluginRunId`) returns it again.

A run is sent as its plugin run id, the duty and job as the game's row ids (`gameContentId`, `gameClassJobId`: [0002](0002-reading-the-game.md#duties-and-jobs-are-the-games-row-ids)), whether it was cleared (`completed`) and a replacement, its wipes and deaths (null when not counted), its time in whole seconds, and when the player entered the duty as `datePlayed`, in UTC. Nothing else (`AGENTS.md`, Privacy).

`MentorRouletteTracker.Core/Api/IMentorApi.cs`, `MentorRouletteTracker.Core/Api/MentorApiClient.cs` (`RunRequest`, `RunResponse`).

## What the site said about a run is optional

- **`newDuty`** is set when it's the user's first run on its duty, with the duties seen out of the duties in the roulette, as the site counts them, and the same for the duty's type in every expansion (`ofType`) and in its expansion (`ofTypeInExpansion`). Those say whether the run finished either; each is null when the duty has no type (or no expansion). It's never set when the site already had the run, so a retried upload isn't announced twice. A seen total is finished only when every one is seen, and never for none of none (0 of 0).
- **`dutyTypeLabel` and `expansionLabel`** are the duty's type and expansion as the site writes them ("Dungeon", "3.0: Heavensward").
- **`pace`** says how the run's time compares with every player's recent runs of its duty, as the site works it out: `verdict`, where it ranks (the site's ids, quickest first: 1 very quick to 5 very slow, so the numbers can't change), and `gapSeconds`, its time minus their median, negative when it was quicker. It's null when the site has nothing to compare it with, when it already had the run, or when the site doesn't say.

All of them are optional, since older versions of the site don't send them. A verdict a newer site adds still binds (any number does), and `ToLoggedRun` drops the pace when the verdict isn't one this version knows (`Enum.IsDefined`), rather than say something wrong.

`MentorRouletteTracker.Core/Api/IMentorApi.cs` (`LoggedRun`, `NewDuty`, `SeenTotals`, `RunPace`, `PaceVerdict`), `MentorRouletteTracker.Core/Api/MentorApiClient.cs` (`RunResponse`, `ToLoggedRun`).

## Every answer is Ok, Unauthorized, Refused or Unavailable

- **Unauthorized** is a 401: the token is missing, unknown or revoked, or its account is disabled.
- **Refused** is a 400, with the site's own message: `{ "error": "…" }`, or the first message of a validation problem's `errors`. Never the problem's `title`, which is HTTP's words ("Bad Request"). Anything else is "The site refused it."
- **Unavailable** is everything else, which is tried again later: the site couldn't be reached, took longer than the client's timeout (`MentorApiClient.Timeout`, 20 seconds), answered with any other failing status (a 5xx, and also a 403, 404 or 429), or answered with a body it couldn't read, or none. A saved address that isn't a web address is Unavailable too. A 502 or 504 comes from the proxy in front of the site when the site itself is down, so it's said like a network failure ("Couldn't reach the site.").

The messages are shown to players, so they never give status codes. Treating every unexpected answer as Unavailable means a run is never failed by a fault that isn't its own; the cost is that a run the site will never take for some reason other than a 400 waits, and holds up the runs after it, until that's fixed.

The plugin's `HttpClient` comes from `CreateHttpClient`, with that timeout and `User-Agent: MentorRouletteTracker/<plugin version>`; tests pass a fake one.

`MentorRouletteTracker.Core/Api/MentorApiClient.cs` (`SendAsync`, `ReadErrorAsync`, `CreateHttpClient`), `MentorRouletteTracker.Core/Api/IMentorApi.cs` (`ApiResultKind`).

## The server address, and the site's former one

The site is `https://mentorroulette.com` (`ServerAddress.Default`). Only a dev build can point at another one (to a dev copy of the site: [development.md](../development.md)). A saved address must be http or https with no query or fragment, and is kept without a trailing slash (`Normalize`, which gives null for anything else, so the settings refuse it). A run's page on the site is `roulettes?run=<site run id>`.

When the plugin loads (`OnLoad`):

- **One of the site's former addresses** (`Former`: `https://mentor.lavask.in`) moves to the default, staying connected (`Move`): the former address still answers and is the same site, so its token still works. Without this, every player would have been disconnected when the address changed.
- **Any other address in the installed plugin** goes back to the default, dropping the token and username (`Reset`), since the token was for that other copy of the site. Dev builds keep their own settings now ([0005](0005-saving.md#a-dev-build-keeps-its-own-files)), but the first dev builds shared the installed plugin's, so a dev site's address could be in them.
- **A dev build keeps its address.**

`MentorRouletteTracker.Core/Api/ServerAddress.cs`, `MentorRouletteTracker/Tracker.cs` (the constructor).

## Nothing waits on the network on the game's frame

Network calls run on the thread pool through `Task.Run`, so even the start of a request (an async method's synchronous part, up to its first `await`) stays off the framework thread. Most go through `Tracker.Background`, which logs a fault ("Couldn't …"). A run that just ended is kept on the framework thread (`Tracker.RunEnded`), then uploaded in the background, and its chat line said once the upload's outcome is known. Connecting checks the token with its own `Task.Run`, so the settings window can show a fault ("Couldn't connect."), and changes the settings on the framework thread.

`MentorRouletteTracker/Tracker.cs` (`Background`, `RunEnded`, `ConnectAsync`).
