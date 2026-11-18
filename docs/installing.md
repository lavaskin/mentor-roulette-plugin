# Installing the plugin

How to install the plugin and connect it to your account on [Mentor Roulette Tracker](https://mentor.lavask.in), up to your first Mentor Roulette run logged.

## Before you start

You need to launch the game with [XIVLauncher](https://goatcorp.github.io/), with Dalamud, its plugin loader, turned on (it is by default).

## 1. Install

The plugin is in its own repository, not Dalamud's official one, so you add that first.

1. In the game, type `/xlsettings` in chat and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this address into the empty box, click **+**, and then **Save and Close**:

   ```
   https://raw.githubusercontent.com/lavaskin/mentor-roulette-plugin/main/repo.json
   ```

3. Type `/xlplugins`, search for **Mentor Roulette Tracker**, and install it.

## 2. Connect

1. On the site, sign in and go to your [Account page's Plugin section](https://mentor.lavask.in/account?section=plugin). Click **Create token**, give it a name (like the PC you play on), click **Create**, and copy the token it shows you. You only see it once.
2. In the game, type `/mrt config`, paste the token into the box and click **Connect**. It says which account it's connected to. One connection covers every character you play on that PC.

## 3. Play

Queue for the Mentor Roulette. When each run ends, cleared or not, it's logged to your account, with a line in chat to say so. Type `/mrt` to see your runs.

Only the Mentor Roulette is ever recorded: other roulettes, and duties you queue for yourself, are left alone.

## More

Uploading runs you played before connecting, when a run counts, and what the plugin sends the site are in [How runs are recorded](runs.md).
