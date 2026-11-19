# Stream Credits Overview & OBS Setup

Stream Credits automatically collects stream events (subscriptions, resubs, gifted subs, follows, cheers, Bits power-ups, incoming raids, channel point redemptions, watch streaks, charity donations, chatters, and bot activities) so you can roll customizable end-of-stream credits directly in OBS or any streaming software.

## Adding Credits to OBS

You have two ways to display credits on stream:

1. **Standalone Credits Browser Source**:
   - Open **Stream Tools → Stream Credits** and click **Copy OBS URL**.
   - In OBS, add a **Browser Source** set to `1920 x 1080` and paste the URL (it includes your `profile` and overlay `key` for secure completion triggers).
   - Enable **Refresh browser when scene becomes active** only if **On Overlay Load** is set to *Auto-Start*; otherwise leave **Wait for Roll Trigger / Action** enabled and trigger credits via **Roll Live**, chat commands, or end-of-stream Actions.

2. **Unified Overlay Editor Widget**:
   - Open **Stream Tools → Overlay Editor** and add a **Credits** widget to any layout.
   - Select the desired Credits Profile and optional `autoStart` / `sampleMode` (`off`, `fake`, or `db`).

## Multiple Profiles

Create separate profiles for different scenes or occasions — for example, a default **Classic Cinema** outro profile, a **Subathon Special** profile using a custom theme and manual checkpoint window, or a **Compact Raid Screen** profile.
