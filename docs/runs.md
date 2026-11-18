# How runs are recorded

## When a run counts

- A pop you decline, withdraw from or let expire records nothing.
- Leaving or being kicked before the clear is a run that wasn't cleared.
- Disconnecting and coming back to the same duty is one run, and the time you were away doesn't count.
- If the game crashes and you don't get back in, the run is recorded as not cleared the next time you log in.
- If you turn the plugin off during a run (or it's reloaded or updated), it can't know how the run ended, so the run is logged only if the plugin's back on in time to see you clear it. Otherwise it isn't logged, and a chat line says so: you can log it on the site. If it's back on before you leave, the time it was off still counts, but any wipes then are missed.

## Your time

Your time is your own time in the duty, to the duty-complete message, or to leaving if it wasn't cleared. It starts at "Duty commenced", so a cleared run's time matches the game's completion time. As a replacement, it starts once you've loaded in. Loading screens, and time disconnected, don't count.

## Uploading

- **Your runs** (`/mrt`) lists every run the plugin has recorded, newest first, uploaded or not. An uploaded run's number links to it on the site.
- **Auto-upload** (on by default) uploads each run when it ends. **Upload N runs** sends every run that isn't uploaded, oldest first. Runs are numbered in the order they reach the site, so runs uploaded late come after any runs you've logged since, but keep the dates they were played.
- **If the site can't be reached,** the run waits, and it tries again when the plugin starts and after your next run. **If the site refuses a run,** it's marked Failed with the reason, and you can retry it. A retry can't log a run twice.
- **Fixing a run:** upload it, then edit it on the site. A run that isn't uploaded can be deleted.
- **Deleting runs:** the plugin keeps every run until you delete it. Settings can delete the uploaded runs, or all of them. Runs already on the site stay there.

## What it keeps and sends

The runs are kept in the plugin's config folder (`pluginConfigs/MentorRouletteTracker/runs.json`), and the token in its settings. It sends the site only the duty, your job, whether it was cleared, whether you were a replacement, the wipes, your time and when the run ended. Never your character's name, or anything about anyone else in the duty.
