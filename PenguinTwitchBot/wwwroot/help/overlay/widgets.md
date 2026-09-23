# Overlay Widget Catalog & Trigger Guide

PenguinTwitchBot includes eight specialized overlay widgets designed for streaming interaction, visual effects, and automated alerts. Each widget communicates with the bot via real-time WebSocket topics on `/ws`.

---

### 1. Alerts (`alerts`)

The **Alerts** widget displays animated on-screen alerts with synced graphics, videos, custom text, and sound effects for stream events.

- **How It Is Triggered**:
  - Alerts are triggered by executing an **Alert** sub-action (`SubActionTypes.Alert`) within any Action.
  - Actions can be bound to any trigger under **Actions & Triggers**, including:
    - **Twitch Events**: Follows (`ChannelFollow`), Cheers/Bits (`ChannelCheer`, `ChannelBitsUse`), Subscriptions (`ChannelSubscribe`), Resubscriptions (`ChannelSubscriptionMessage`), Gifted Subs (`ChannelSubscriptionGift`), Raids (`ChannelRaid`), Channel Point Redemptions, and Ad Breaks.
    - **Chat Commands**: Custom commands (e.g. `!hype`).
    - **Timers & Keywords**: Timed events or chat keyword matches.
- **Alert Channels (`alertChannel`)**:
  - The alert widget supports optional channel routing.
  - In the Overlay Editor, you can specify an Alert Channel name (e.g. `gameplay`, `subathon`, `side-cam`).
  - When configuring the Alert sub-action, matching channel names will only display on that specific widget instance. Alerts without a channel route to default alert widgets.
- **Queue & Media Processing**:
  - The widget queues incoming alerts so rapid events (like sub bombs or raid trains) play sequentially without overlapping.
  - Supports WebM video (with transparency), MP4, animated GIFs, PNGs, and audio files (WAV, MP3, OGG).
  - Custom CSS styling and dynamic text templating with variable replacement (`{User}`, `{DisplayName}`, `{Amount}`, `{Tier}`, etc.).
  - Emote walls: Can trigger floating animated emote explosions alongside alerts.

---

### 2. Fireworks (`fireworks`)

A high-performance particle canvas simulation that launches realistic celebratory fireworks over your stream.

- **How It Is Triggered**:
  - **Triggered Only via Sub-Actions**: Fireworks are triggered *exclusively* via sub-actions (specifically an `ExecuteAction` sub-action targeting a Fireworks action). When executed through the Action Queue, the bot broadcasts a WebSocket event (`data.name == "Fireworks"`) on the `events` topic, launching the burst animation.
  - **Sample Actions Available**: Pre-built sample actions for fireworks (including bursts for 5, 10, 25, 50, 100, and Epic fireworks, tier-based sub celebrations, and `!fireworks` triggers) are provided in `Data/SampleActions/ActionGroup_Fireworks.json` and can be imported via [Manage Actions](/actions/manage).
  - **Ambient Auto-Launch**: Can also be set to `AutoLaunch = true` in widget settings to continuously launch ambient background fireworks during celebrations.
- **Customizable Settings**:
  - **Shell Type**: Choose burst designs including `Crackle`, `Crossette`, `Crysanthemum`, `Falling Leaves`, `Floral`, `Ghost`, `Horse Tail`, `Palm`, `Ring`, `Strobe`, `Willow`, or `Random`.
  - **Explosion Size & Scale**: Adjust particle count and spread diameter.
  - **Long Exposure**: Toggles light trail persistence for luminous firework effects.
  - **Quality Modes**: `auto`, `high`, or `force low-end` for optimized GPU/CPU performance inside OBS browser sources.
  - **Canvas Delay**: Automatically clears the canvas after bursts finish to free resources.

---

### 3. Clips Player (`clips`)

Displays and plays Twitch clips directly on stream inside your overlay layout.

- **How It Is Triggered**:
  - Triggered via clip queue sub-actions, chat commands (e.g. `!clip`), or channel point redemption rewards.
- **Features**:
  - Subscribes to the `clips` WebSocket topic.
  - Seamlessly buffers, fades in, plays clip video and audio, and cleanly fades out upon completion.
  - Can be sized and positioned to fill fullscreen or occupy a designated window box on your screen.

---

### 4. Fishing Mini-Game (`fishing`)

Provides real-time visual feedback for the chat-based fishing mini-game.

- **How It Is Triggered**:
  - **Triggered Only via Sub-Actions**: The fishing overlay widget is triggered *exclusively* by executing the **Fishing** sub-action (`SubActionTypes.Fishing`). When an action containing this sub-action runs, it calculates the catch, rolls rarity, awards XP, and broadcasts the event over the `fishing` WebSocket topic.
  - **Sample Actions Available**: Complete sample actions for fishing (including casting mechanics, chat responses for catches and accidents, and point/gold payouts) are provided in `Data/SampleActions/ActionGroup_Fishing.json` and can be imported via [Manage Actions](/actions/manage).
- **Features**:
  - Subscribes to the `fishing` WebSocket topic.
  - Displays the catching viewer's avatar and username.
  - Animated rod reel visual, species artwork, weight, and rarity badge (Common, Uncommon, Rare, Epic, Legendary).
  - Displays bonus items, experience gain, and level-up milestone celebrations.

---

### 5. Fishing Tournaments (`fishing_tournaments`)

Renders a dynamic live tournament leaderboard directly on your stream overlay.

- **How It Is Triggered**:
  - Automatically triggered by fishing tournament lifecycle events:
    - `FishingTournamentStart`: Initializes the tournament display and countdown timer.
    - `FishingTournamentCatch`: Instantly updates scores when an eligible tournament catch occurs.
    - `FishingTournamentEnd`: Displays final winner standings and settlement rewards.
- **Features**:
  - Shows remaining tournament time, target species criteria, top 5 ranked participants, and catch points in real time.

---

### 6. Prize Wheel (`wheel`)

An interactive spinning wheel widget for giveaways, viewer choices, and channel point spins.

- **How It Is Triggered**:
  - **Chat Commands**: Can be controlled directly from chat using built-in streamer commands:
    - `!showwheel <id>`: Displays the specified custom wheel on the overlay.
    - `!hidewheel`: Hides the active wheel.
    - `!spinwheel`: Spins the visible wheel with weighted random segment selection.
    - `!opennamewheel`: Opens a viewer entry wheel where chat members join via `!join`.
    - `!shownamewheel`: Shows the viewer name wheel on stream.
    - `!closenamewheel`: Closes entries for the name wheel.
    - `!spinnamewheel`: Spins the viewer name wheel to choose a random winner.
  - **Sub-Actions**: Can be triggered via Sub-Actions within any Action (e.g. Channel Point redemptions, subathons, or automated giveaways).
- **Features**:
  - Subscribes to the `wheel` WebSocket topic.
  - Configurable slices, colors, labels, deceleration physics, and pointer tick audio.
  - Emits winner results back to the bot to trigger follow-up actions (e.g. chat announcement, reward payout).

---

### 7. Stream Chat (`chat`)

A customizable stream chat overlay that renders Twitch chat on your broadcast.

- **How It Is Triggered**:
  - Subscribes to the `chat` WebSocket topic; updates in real time on every incoming Twitch IRC/EventSub message.
- **Features**:
  - Renders official Twitch badges (broadcaster, moderator, VIP, subscriber tiers) and emotes.
  - Supports third-party emotes: **7TV**, **BetterTTV (BTTV)**, and **FrankerFaceZ (FFZ)**.
  - Direction: Vertical scrolling or horizontal ticker.
  - Layout & Alignment: Top or bottom vertical alignment.
  - Message Timeout: Automatically fade out messages after a configurable number of seconds, or keep them permanently.
  - Moderation Sync: Purged or banned viewer messages are immediately removed from the on-screen overlay.
  - Filters: Hide bot commands (messages beginning with `!`) or ignore specified usernames.
  - Styling: Fully configurable font family, size, weight, colors, and row backgrounds.

---

### 8. Stream Timer (`timer`)

A synchronized countdown or countup timer widget.

- **How It Is Triggered**:
  - Controlled from the [Stream Timer](/overlay/timer) dashboard or via timer sub-actions:
    - `OverlayTimerStart`
    - `OverlayTimerStop`
    - `OverlayTimerAddTime`
    - `OverlayTimerRemoveTime`
- **Features**:
  - Subscribes to the `overlay` WebSocket topic for real-time synchronization.
  - Formats: Auto, `hh:mm:ss`, or `mm:ss`.
  - Prefix / Suffix labels (e.g. `"SUBATHON: "` or `" REMAINING"`).
  - Option to show tenths of a second.
  - Visibility toggles: Hide when stopped, or hide when timer reaches zero.
  - Fully customizable typography, colors, shadows, borders, and letter spacing.
