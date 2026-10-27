[![.NET Linux](https://github.com/Psychoboy/PenguinTwitchBot/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Psychoboy/PenguinTwitchBot/actions/workflows/dotnet.yml)
[![.NET Windows](https://github.com/Psychoboy/PenguinTwitchBot/actions/workflows/dotnet-win.yml/badge.svg)](https://github.com/Psychoboy/PenguinTwitchBot/actions/workflows/dotnet-win.yml)
[![CodeFactor](https://www.codefactor.io/repository/github/psychoboy/penguintwitchbot/badge)](https://www.codefactor.io/repository/github/psychoboy/penguintwitchbot)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/3e4574fdb5b9423fb850c40b5d4a14aa)](https://app.codacy.com/gh/Psychoboy/PenguinTwitchBot/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)

# PenguinTwitchBot

A feature-rich, self-hosted Twitch bot and web dashboard built on .NET 10. Cross-platform - runs on Windows, Linux, and macOS.

> **Early Testing Notice:** This project is currently in early testing. Bugs are expected. Please report any issues you encounter on the [GitHub Issues](https://github.com/Psychoboy/PenguinTwitchBot/issues) page.

> **AI Disclosure:** Limited AI assistance was used for small refactors, bug fixes, and this document.

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Quick Start (Pre-built Release)](#quick-start-pre-built-release)
  - [Step 1: Run the Setup Wizard](#step-1-run-the-setup-wizard)
  - [Step 2: Run the Bot](#step-2-run-the-bot)
- [Database Support](#database-support)
- [Optional Integrations](#optional-integrations)
- [Web Dashboard](#web-dashboard)
- [Raffles](#raffles)
- [OBS Browser Sources](#obs-browser-sources)
- [Twitch Panel Extension](docs/twitch-extension-setup.md)
- [REST API & Stream Deck Integration](#rest-api--stream-deck-integration)
- [Contributing / Building From Source](#contributing--building-from-source)
- [License](#license)

---

## Features

### Chat & Viewer Engagement
- **Custom & Default Commands** - Create, edit, and manage chat commands with cooldowns, aliases, rank permissions, and per-user/global limits
- **Multi-Currency Loyalty Points** - Configure multiple custom point currencies (e.g., Points, Coins, Tickets) earned passively by watching, via Twitch events (subs, cheers, raids), or through `!claim` attendance bonuses
- **Outgoing Raid Rewards** - Track and automatically reward viewers who stay and participate in outgoing raids to other channels
- **Quote System** - Save and recall memorable quotes from chat
- **Shoutout System** - Automated and manual shoutouts (`!so`) for raiders and VIPs with clip playback and optional AI-generated intros
- **Raid Tracker** - Tracks incoming raids, raider history, and triggers custom raid actions
- **Last Seen & Viewer Profiles** - Detailed viewer profiles, watch time, chat history, and moderation utilities
- **Death Counters & Custom Counters** - Stream counters manageable from chat, sub-actions, or the dashboard with live overlay widgets
- **Top Lists** - Display top viewers by points, watch time, and more
- **Auto Timers** - Timer groups that run actions or send periodic messages to chat on a schedule
- **Markov Chain Chat** - AI-style chat generation based on your channel's chat history
- **Blacklist / Moderation** - Word, phrase, and regex blacklisting with interactive filter testing and configurable timeout penalties

### Actions, Triggers & Automation Engine
- **Multi-Trigger Workflows** - Bind actions to Twitch EventSub events (Follows, Subs, Resubs, Gift Subs, Raids, Cheers/Bits, Channel Point Redemptions, Hype Trains, Ad Breaks), Chat Commands, Keywords, Timers, Hotkeys, Fishing events, and Minigame Outcomes
- **Rich Sub-Action Pipeline** - Execute steps sequentially or concurrently: send/reply in chat, manage viewer points (`ManagePoints`, `CheckPoints`, `GiftPoints`), branch with `Logic: If/Else`, loop with `ForEachViewer`, control OBS Studio via WebSocket, play audio/TTS, trigger overlay alerts/fireworks, manage stream timers, call external HTTP APIs, write files, or query OpenAI
- **One-Click Game Outcome Automations** - Built-in setup wizard on every minigame page to attach point payouts, chat announcements, or custom actions to Win, Bullseye, Podium, Knockout, Lose, or Match Concluded triggers with advanced trigger filters
- **Context-Aware Variable Inspector** - Interactive **Available Variables** drawer in the action editor with click-to-copy `%Variable%` tokens, `$math(...)` expression evaluation, and dedicated **Catch SubActions** error handling (`%ActionErrorMessage%`)

### Stream Overlay Minigames
- **Parachute Drop (`!drop`, `!droplets`, `!rockets`)** - 60 FPS canvas skydiving game with wind drift, mid-air nudging droplets, canopy-burning sky rockets, and landing accuracy scoring
- **Plinko Bounce (`!plinko`)** - Physics pegboard drop game with customizable prize/multiplier buckets, peg bounciness, and horizontal deflection
- **Balloon Ascent (`!balloon`, `!hazards`)** - Rising balloon accuracy game where viewers float toward a sky arch while dodging droplets, heavy anvils, wind gusts, and popping darts
- **Curling / Shuffleboard (`!curl`)** - Non-intrusive screen-edge sheet where viewers slide stones or pucks across Slick, Normal, or Heavy Frost ice with stone-on-stone collisions and house ring scoring
- **Golf — Chip & Putt (`!golf`, `!ball`)** - Screen-edge golf minigame featuring Wedge, Iron, and Putter trajectories, water hazards, sand bunkers, cup lip-outs, hole-in-one celebrations, and multi-stroke play
- **Racing & Destruction Derby (`!race`, `!timetrial`)** - Perimeter ring, edge oval, figure-8, drag strip, and Derby Bowl crash arena racing supporting Grand Prix, Last Kart Elimination, Destruction Derby (kart HP, sparks/smoke/fire, knockouts), automatic multi-heat tournament brackets, session queueing, and solo hot-lap time trials
- **Starship Expedition (`!sj`, `!sv`, `!starship`)** - Cooperative long-form space voyage where viewers board the crew, manage shared ship Hull & Supplies, visit space stations, and vote on branching multi-step narrative events with custom event/sector editors and rarity reward profiles
- **UFO Claw Machine (`!joinclaw`)** - Arcade mechanical claw machine overlay for drawing prizes from weighted prize sets or selecting random viewers with suspense feints and reveal triggers
- **Seasonal Themes & 60 FPS Emotes** - Six built-in seasonal themes (Spring, Summer, Fall, Winter, Halloween, Christmas), custom theme media/audio uploads, and automatic Twitch, 7TV, BTTV, and FFZ animated emote resolution

### Chat Games & Gambling
- **Fishing Game** - Full-featured fishing RPG with rod/bait/trap shop, item durability & repair, rarity tiers, live overlay tournaments, and economy balance simulator
- **Duels, Gamble, Roulette, Slots, Steal, Defuse, Heist, & Free For All (`!ffa`)** - Classic interactive chat games wagering channel points
- **Raffles & Giveaways** - Configurable SubAction-based raffles and ticket giveaways
- **Wheel Spin** - Interactive spinning prize wheels and viewer name wheels (`!join`)

### Media & Integrations
- **Song Requests & Multi-Playlist Player** - YouTube-based song request queue (`!sr`), playlist pooling, banned song filters, and song cooldowns
- **Text-to-Speech (TTS)** - Read chat messages, alerts, or channel point redemptions aloud with per-user assigned voices
- **OBS Studio WebSocket Integration** - Switch scenes, toggle sources/filters, mute audio, update text/browser sources, and trigger hotkeys
- **Discord Integration** *(optional)* - Go-live announcements, live member role assignment, and chat bridge
- **Weather Commands** *(optional)* - `!weather` command powered by WeatherAPI.com
- **AI Chat Responses** *(optional)* - OpenAI-powered chat responses, sub-actions, and automated shoutouts
- **Channel Point Redeems** - Create, enable/pause, and bind Twitch Channel Point rewards directly to bot Actions
- **Clip Tracking** - Automatically downloads and plays back Twitch clips using `yt-dlp` (bundled)

---

## Requirements

- A computer to host the bot (Windows, Linux, or macOS)
- A Twitch account for the **bot** (can be the same as your streamer account)
- A Twitch Developer Application ([dev.twitch.tv](https://dev.twitch.tv/console))
- .NET 10 Runtime - only needed if running from the compiled DLL; the self-contained Windows release bundles the runtime

---

## Quick Start (Pre-built Release)

Download the latest release from the [Releases page](https://github.com/Psychoboy/PenguinTwitchBot/releases) and extract it to a folder of your choice.

### Step 1: Run the Setup Wizard

Run **`PenguinTwitchBot.Setup.exe`** (Windows) or `PenguinTwitchBot.Setup` (Linux/macOS).

> **Linux/macOS:** You may need to mark the file as executable first: `chmod +x PenguinTwitchBot.Setup`

The setup wizard is a browser-based application that walks you through every required and optional setting. It will open a browser window automatically. If it does not, navigate to `http://localhost:5000`.

The wizard covers the following steps:

| Step | Description |
|------|-------------|
| 1 | **Welcome** - overview of what you need |
| 2 | **Bot Identity** - your bot's Twitch username and your channel name |
| 3 | **Twitch Streamer App** - Client ID and Secret from [dev.twitch.tv](https://dev.twitch.tv/console/apps/create) |
| 4 | **Authorize Streamer Account** - OAuth authorization via your browser |
| 5 | **Authorize Bot Account** *(optional)* - OAuth authorization for your bot account; grants `user:write:chat` and `user:bot` scopes required for sending chat messages |
| 6 | **Database** - choose SQLite or PostgreSQL (see [Database Support](#database-support)) |
| 7 | **YouTube API** *(optional)* - enables the song request feature |
| 8 | **Discord Integration** *(optional)* - bot token, server ID, and channel IDs |
| 9 | **Weather** *(optional)* - WeatherAPI key and default location |
| 10 | **OpenAI** *(optional)* - API key for AI-powered chat features |
| 11 | **Review & Save** - review all settings (secrets masked) and write `appsettings.secrets.json` |

When the wizard finishes, it saves your configuration to `appsettings.secrets.json` in the bot's directory and shuts itself down automatically.

#### Authorizing the bot account (Step 5)

The bot account requires a **user access token** with the `user:write:chat` and `user:bot` scopes — a plain app token is not sufficient.

Step 5 provides two options:

- **Open Auth Page in Browser** — opens the Twitch authorization page directly. Twitch will show a sign-in prompt (forced by `force_verify=true`) so you can switch accounts if needed.
- **Copy Auth URL** — generates the authorization URL and displays it in a text field so you can copy and paste it into a different browser or incognito window where your bot account is already signed in.

> **Tip:** If your bot and streamer accounts are different Twitch accounts, use **Copy Auth URL** and paste it into a browser session where the bot account is logged in.

#### Creating a Twitch Developer Application

1. Go to [dev.twitch.tv/console/apps/create](https://dev.twitch.tv/console/apps/create) and log in with your **streamer** account.
2. Give the app a name (e.g. `MyBot`).
3. Add the following OAuth Redirect URL:
   - `http://localhost:5000/redirect`

  > **Running on a different host or port?** Use your actual bot URL and port in the redirect URL above.
   >
   > **LAN hostname access:** If users connect from another machine using your host name (for example, `https://your-bot-host:5001`), add that exact redirect URL in Twitch Developer Console too (for example, `https://your-bot-host:5001/redirect`). Use lowercase for the host name when entering redirect URLs and when sharing the URL with users.

   > **Direct IP Access:** Twitch only allows non-HTTPS OAuth redirects for `localhost` and `127.0.0.1`. If you plan to access the bot directly by IP address, add an HTTPS endpoint under the `Kestrel:Endpoints` section in `PenguinTwitchBot/appsettings.json`.
   >
   > Example:
   > ```json
   > "Kestrel": {
   >   "Endpoints": {
   >     "Http": {
   >       "Url": "http://0.0.0.0:5000"
   >     },
   >     "Https": {
   >       "Url": "https://0.0.0.0:5001"
   >     }
   >   }
   > }
   > ```
   >
   > Then add the matching redirect URL in your Twitch Developer Console application settings, for example `https://YOUR_IP:5001/redirect`.
   >
   > The bot will generate a self-signed HTTPS certificate automatically if you do not provide one, so the app can start without the ASP.NET Core developer certificate. If you browse to the bot directly by IP, your browser may still warn about the certificate unless you trust it or use a certificate issued for that address.

4. Set the category to **Chat Bot** and click **Create**.
5. Click **Manage** → **New Secret** to generate your Client Secret.
6. Copy the **Client ID** and **Client Secret** into the setup wizard.

---

### Step 2: Run the Bot

Once setup is complete, run **`PenguinTwitchBot.exe`** (Windows) or `PenguinTwitchBot` (Linux/macOS).

> **Linux/macOS:** You may need to mark the file as executable first: `chmod +x PenguinTwitchBot`

The bot starts a web server. Open your browser to `http://localhost:5000` (or the port shown in the console) to access the **web dashboard**.

On first launch, the database will be automatically created and migrated. The bot will then connect to Twitch and begin operating.

> **Tip:** If you skipped the bot account authorization in the setup wizard, navigate to `/settings/bot-auth` (or `/botsignin`) in the dashboard to review connection status and authorize your accounts.

> **HTTPS note:** If you enable the `Https` endpoint for direct-IP access, the bot will generate a self-signed certificate automatically if you do not supply one. That avoids the startup failure, but browsers may still warn about certificate trust when you open the bot by IP address.

> **Direct IP Access:** If users will browse to the bot with a direct IP address instead of `localhost`, Twitch sign-in must use HTTPS. Add the `Https` entry under `Kestrel:Endpoints` in `PenguinTwitchBot/appsettings.json` and then update your Twitch Developer Console redirect URLs to match the exact HTTPS address users will open.
>
> Example `PenguinTwitchBot/appsettings.json` section:
> ```json
> "Kestrel": {
>   "Endpoints": {
>     "Http": {
>       "Url": "http://0.0.0.0:5000"
>     },
>     "Https": {
>       "Url": "https://0.0.0.0:5001"
>     }
>   }
> }
> ```
>
> Matching Twitch redirect example: `https://YOUR_IP:5001/redirect`
>
> **LAN access recommendation (Music Player / YouTube):** Prefer opening the bot with the machine hostname on your LAN (for example, `https://your-bot-host:5001`) instead of a raw IP (for example, `https://192.168.x.x:5001`). Some YouTube embeds are more likely to fail with API error `150` when accessed by direct IP origin.

OAuth redirects now use the URL/host/port from the request used to access the bot, so no separate URL configuration setting is required.

---

## Database Support

The bot supports two database backends. You select your preference during the setup wizard.

| Database | Notes |
|----------|-------|
| **SQLite** *(default)* | No external server required. Stores everything in a single file (`Data/PenguinTwitchBot.sqlite`). Recommended for most users. |
| **PostgreSQL** | Requires a running PostgreSQL server. Provide a connection string in the wizard. |

The database is created and migrated automatically on startup. You can switch databases later by editing `appsettings.secrets.json` and re-running the bot.

---

## Optional Integrations

All optional integrations can be configured during the setup wizard or added later by editing `appsettings.secrets.json`.

### YouTube / Song Requests
Requires a **YouTube Data API v3** key from [Google Cloud Console](https://console.cloud.google.com/apis/library/youtube.googleapis.com). Used to search and queue YouTube videos for the song request feature.

If the dashboard is opened from another machine on your LAN, use the bot host name (for example, `https://your-bot-host:5001`) rather than a raw IP address when possible. This avoids a YouTube embed edge case where some videos can fail with player error `150` on direct IP origins.

#### Troubleshooting: YouTube Error 150 on LAN

If songs play on `localhost` but skip for another LAN user, try the following:

1. Open the dashboard using the host machine name (for example, `https://your-bot-host:5001`) instead of `https://192.168.x.x:5001`.
2. Use the same host name in any saved bookmarks, overlays, or embedded browser sources.
3. Hard refresh the browser (`Ctrl+F5`) after changing the URL.
4. If OAuth is used on LAN/remote access, add the exact machine-name URL in Twitch Developer Console (for example, `https://your-bot-host:5001/redirect`) and make sure users open that same host/port.

This behavior is usually caused by YouTube embed-origin restrictions, not by your YouTube Data API key itself.

### Discord
Requires a **Discord bot token** from [discord.com/developers](https://discord.com/developers/applications). Enable **Server Members Intent** and **Message Content Intent** in the bot settings. Features include:
- Go-live announcements to a designated channel
- Automatic role assignment while you are live
- Role pinging on stream start

### Weather
Requires a free **WeatherAPI key** from [weatherapi.com](https://www.weatherapi.com/signup.aspx). Enables the `!weather` command in chat.

### OpenAI
Requires an **OpenAI API key** from [platform.openai.com](https://platform.openai.com/api-keys). Enables AI-powered chat responses and automated shoutouts. Usage is billed by token - set spend limits in your OpenAI account dashboard.

### Google Text-to-Speech (TTS)
Enables the `!say` command and Actions to read messages aloud using Google Cloud's high-quality text-to-speech voices. 

**Setup Instructions:**

1. Create a **Google Cloud Project**:
   - Go to [console.cloud.google.com](https://console.cloud.google.com/)
   - Click **Select a Project** → **New Project**
   - Name it (e.g., `PenguinTwitchBot`) and click **Create**

2. **Enable the Text-to-Speech API**:
   - Once your project is created, navigate to [APIs & Services](https://console.cloud.google.com/apis/library)
   - Search for "Text-to-Speech API"
   - Click on it and press **Enable**

3. **Create a Service Account**:
   - Go to [Service Accounts](https://console.cloud.google.com/iam-admin/serviceaccounts)
   - Click **Create Service Account**
   - Enter a name (e.g., `PenguinBot`) and click **Create and Continue**
   - Skip "Grant this service account access to the project" (click **Continue**)
   - Skip "Grant users access to this service account" (click **Done**)

4. **Generate a JSON Key**:
   - In the Service Accounts list, find your newly created service account and click on it
   - Go to the **Keys** tab
   - Click **Add Key** → **Create new key** → choose **JSON**
   - A JSON file will download automatically

5. **Add the Key to Your Bot**:
   - Rename the downloaded JSON file to **`gtts.json`**
   - Place it in the bot's root directory (same folder as `PenguinTwitchBot.exe` / `PenguinTwitchBot`)
   - The file will be automatically detected on next startup

> **Cost Note:** Google Cloud Text-to-Speech has a free tier (500,000 characters/month). Check [pricing](https://cloud.google.com/text-to-speech/pricing) for details. Set up billing alerts in your Google Cloud Console to avoid surprises.

---

## Web Dashboard

The bot includes a web-based dashboard accessible at `http://localhost:5000` (default) with contextual **Help (`?`)** guides on every page. From the dashboard you can:

- Design multi-widget stream layouts in the visual drag-and-drop **Overlay Editor** (`/overlay/editor`)
- Build and test automated workflows in **Manage Actions** (`/actions/manage`) with triggers, sub-actions, queues, and execution history
- Configure and test **Stream Overlay Minigames** (Parachute Drop, Plinko Bounce, Balloon Ascent, Curling, Golf, Racing & Destruction Derby, Starship Expedition, UFO Claw, and Wheel Spin) and **Seasonal Themes**
- Set up one-click **Outcome Automations** for minigame wins, bullseyes, podiums, knockouts, and completions
- View and manage default, custom, action, audio, and external chat commands and aliases
- Configure multiple viewer point currencies, passive earning rates, loyalty bonuses, and outgoing raid rewards
- Manage the fishing game (fish catalog, shop items, image processing, tournaments, and balance analysis)
- Configure auto timers, auto-shoutouts, stream timers, and counters
- Manage the YouTube song request player, multi-playlist pools, banned songs, and cooldowns
- Configure Twitch Channel Point rewards, OBS WebSocket connections, Discord, TTS voices, and OpenAI integrations
- Run database backups, inspect action validation health checks, and manage application updates & live logs

---

## Raffles

Raffles are now built from the same Action and SubAction system used elsewhere in the bot.

- Create fully custom raffle flows with `Raffle Start`, `Raffle Enter`, `Raffle End`, and the related raffle subactions.
- Run multiple raffles at the same time by giving each raffle its own key and commands.
- Use the `Create Raffle` button in Action Management to scaffold a full raffle setup quickly.

Full setup guide: [PenguinTwitchBot/Docs/Raffles.md](PenguinTwitchBot/Docs/Raffles.md)

---

## OBS Browser Sources

The bot serves several overlay pages designed to be added as **Browser Sources** in OBS (or any other streaming software that supports browser sources). All URLs are relative to the bot's address - `http://localhost:5000` by default.

To add any of these in OBS:
1. In a Scene, click **+** under Sources → **Browser**.
2. Paste the URL from the table below.
3. Set the width/height to match your canvas (typically 1920×1080).
4. Enable **"Shutdown source when not visible"** and **"Refresh browser when scene becomes active"** as needed.
5. Check **"Use custom frame rate"** only if you need to match a specific frame rate.

> For a clean overlay, make sure **"Control audio via OBS"** is enabled if the source plays audio (clips, TTS, alerts).

### Available Overlays

You can combine multiple widgets into a single browser source using the **Overlay Editor** (`/overlay/editor`), or add individual widget pages directly:

> **Overlay Security Key:** When overlay security is enabled, copy the URL using the **Copy URL** / **Open Overlay** button in the dashboard so the `?key=YOUR_OVERLAY_KEY` query parameter is included automatically. Interactive overlays (such as minigames and UFO Claw) require this key to report completion results back to the bot.

| Overlay | URL | Recommended Size | Description |
|---------|-----|------------------|-------------|
| **Unified Overlay Compositor** | `http://localhost:5000/overlay.html?layout=Default&key=...` | `1920×1080` (or canvas size) | Renders all widgets positioned in your **Overlay Editor** layout over a shared WebSocket connection. |
| **Alerts** | `http://localhost:5000/alerts.html` | `1920×1080` | Displays follow, sub, raid, cheer, and custom action alerts with images, video, sounds, text animations, and emote walls. |
| **Fireworks** | `http://localhost:5000/fireworks.html` | `1920×1080` | Launches customizable particle firework bursts over your stream when triggered by actions. |
| **Clips** | `http://localhost:5000/clips.html` | `1920×1080` | Plays back downloaded Twitch clips with clipper and game info. |
| **Stream Chat** | `http://localhost:5000/chat.html` | `400×600` | Renders live Twitch chat with badges and Twitch/7TV/BTTV/FFZ emotes in vertical or horizontal ticker mode. |
| **Stream Timer** | `http://localhost:5000/timer.html` | `420×160` | Synchronized countdown/countup timer controlled from the dashboard or timer sub-actions. |
| **Counters** | `http://localhost:5000/counter.html` | `400×160` | Live on-screen display for death counters and custom named counters. |
| **Fishing** | `http://localhost:5000/fishing.html` | `500×300` (or `1920×1080`) | Real-time fishing catch notifications, fish artwork, rarity badges, and XP progress. |
| **Fishing Tournaments** | `http://localhost:5000/fishing-tournaments.html` | `620×380` | Live tournament leaderboard and countdown timer during active fishing tournaments. |
| **Wheel Spin** | `http://localhost:5000/wheel.html` | `1920×1080` | Interactive spinning prize wheel and viewer name wheel. |
| **UFO Claw Machine** | `http://localhost:5000/ufo.html` | `1920×1080` | Arcade mechanical claw machine for prize set draws and viewer draws (`!joinclaw`). |
| **Parachute Drop** | `http://localhost:5000/parachute.html` | `1920×1080` | Parachute Drop minigame (`!drop`, `!droplets`, `!rockets`). |
| **Plinko Bounce** | `http://localhost:5000/plinko.html` | `1920×1080` | Plinko Bounce pegboard minigame (`!plinko`). |
| **Balloon Ascent** | `http://localhost:5000/balloon.html` | `1920×1080` | Balloon Ascent sky-arch minigame (`!balloon`, `!hazards`). |
| **Curling / Shuffleboard** | `http://localhost:5000/curling.html` | `1920×1080` | Edge-strip Curling & Shuffleboard minigame (`!curl`). |
| **Golf (Chip & Putt)** | `http://localhost:5000/golf.html` | `1920×1080` | Edge-strip Golf Chip & Putt minigame (`!golf`, `!ball`). |
| **Racing & Destruction Derby** | `http://localhost:5000/racing.html` | `1920×1080` | Perimeter/edge/arena Racing & Destruction Derby minigame (`!race`, `!timetrial`). |
| **Starship Status** | `http://localhost:5000/starship-status.html` | `520×180` | Compact Starship Expedition status panel showing ship, sector, hull/supplies, crew, and next-event countdown. |
| **Starship Event** | `http://localhost:5000/starship-event.html` | `620×360` | Starship Expedition boarding, branching crew vote options (`!sv`), live vote bars, and outcome stories. |

---

## REST API & Stream Deck Integration

The bot exposes a small REST API that lets you trigger any chat command externally - from a Stream Deck button, a macro pad, a script, or any tool that can make HTTP requests.

### Endpoint

```
PUT http://localhost:5000/commands
```

| Header | Required | Description |
|--------|----------|-------------|
| `webauth` | Yes | The `webauth` token from your `appsettings.secrets.json`. Auto-generated by the setup wizard. |
| `user` | Yes | The Twitch username the command runs as (e.g. your channel name). Granted broadcaster, mod, and sub permissions automatically. |
| `message` | Yes | The full command string exactly as you would type it in chat (e.g. `!death` or `!so mycoolstreamer`). |

Returns `200 OK` on success, `403 Forbidden` if the `webauth` token is wrong.

#### Finding your webauth token

Open `appsettings.secrets.json` in the bot's folder and copy the value of the `webauth` key. It is generated automatically when you run the setup wizard.

### curl Example

```bash
curl -X PUT http://localhost:5000/commands \
  -H "webauth: YOUR_WEBAUTH_TOKEN" \
  -H "user: yourchannelname" \
  -H "message: !death +"
```

On Windows (Command Prompt):

```cmd
curl -X PUT http://localhost:5000/commands -H "webauth: YOUR_WEBAUTH_TOKEN" -H "user: yourchannelname" -H "message: !death +"
```

### Stream Deck Integration

Stream Deck does not natively support PUT requests, but there are two easy approaches:

#### Option A - Script + Open action (built-in, no plugins needed)

1. Create a script file in a convenient location.

   **Windows - `increment-death.bat`:**
   ```bat
   @echo off
   curl -X PUT http://localhost:5000/commands -H "webauth: YOUR_WEBAUTH_TOKEN" -H "user: yourchannelname" -H "message: !death +"
   ```

   **Linux / macOS - `increment-death.sh`:**
   ```bash
   #!/bin/bash
   curl -X PUT http://localhost:5000/commands \
     -H "webauth: YOUR_WEBAUTH_TOKEN" \
     -H "user: yourchannelname" \
     -H "message: !death +"
   ```
   On Linux/macOS run `chmod +x increment-death.sh` once to make it executable.

2. In Stream Deck, add a **System: Open** action (Windows) or **System: Website** action and point it at your script file.
   - On Windows you can also use **System: Run** → `cmd /c "C:\path\to\increment-death.bat"`
   - On macOS use **System: Open** and select the `.sh` file.

3. Give the button an icon and label, and you're done - pressing it will trigger the command in your Twitch chat.

#### Option B - HTTP Request plugin (Elgato Marketplace)

The [Elgato Marketplace](https://marketplace.elgato.com/stream-deck) has several community plugins that can send arbitrary HTTP requests directly (search for *"webhook"* or *"HTTP request"*). If you use one of these:

- Method: `PUT`
- URL: `http://localhost:5000/commands`
- Headers:
  - `webauth: YOUR_WEBAUTH_TOKEN`
  - `user: yourchannelname`
  - `message: !yourcommand`

---

## Contributing / Building From Source

Contributions are welcome. The project is cross-platform and builds on Windows, Linux, and macOS.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

### Clone and Build

```bash
git clone https://github.com/Psychoboy/PenguinTwitchBot.git
cd PenguinTwitchBot

# Restore and build
dotnet build PenguinTwitchBot/PenguinTwitchBot.csproj

# Run the setup wizard
dotnet run --project PenguinTwitchBot.Setup/PenguinTwitchBot.Setup.csproj

# Run the bot
dotnet run --project PenguinTwitchBot/PenguinTwitchBot.csproj
```

### Publish (Self-Contained)

```bash
# Windows x64
dotnet publish PenguinTwitchBot/PenguinTwitchBot.csproj -c Release -r win-x64 --self-contained true

# Linux x64
dotnet publish PenguinTwitchBot/PenguinTwitchBot.csproj -c Release -r linux-x64 --self-contained true

# macOS arm64 (Apple Silicon)
dotnet publish PenguinTwitchBot/PenguinTwitchBot.csproj -c Release -r osx-arm64 --self-contained true
```

### Project Structure

| Project | Description |
|---------|-------------|
| `PenguinTwitchBot/` | Main bot service, Blazor Server web dashboard, and HTML5/Canvas stream overlays |
| `PenguinTwitchBot.Database/` | EF Core database context, entities, repositories, and action/sub-action/trigger models |
| `PenguinTwitchBot.Migrations.Sqlite/` | EF Core migrations for SQLite |
| `PenguinTwitchBot.Migrations.Postgres/` | EF Core migrations for PostgreSQL |
| `PenguinTwitchBot.TwitchApi/` | Twitch Helix API, EventSub WebSocket, and IRC client library |
| `PenguinTwitchBot.Setup/` | First-time browser-based setup wizard |
| `PenguinTwitchBot.Updater/` & `PenguinTwitchBot.Updater.Bootstrap/` | Self-updater and bootstrap utilities |
| `PenguinTwitchBot.Test/` & `PenguinTwitchBot.TwitchApi.Test/` | Unit and integration test suites |

### Running Tests

```bash
dotnet test PenguinTwitchBot.Test/PenguinTwitchBot.Test.csproj
```

### Docker

A `Dockerfile` is included in the `PenguinTwitchBot/` directory for containerized deployments.

```bash
docker build -f PenguinTwitchBot/Dockerfile -t penguintwitchbot .
docker run -p 5000:5000 -v ./data:/app/Data penguintwitchbot
```

### Guidelines

- Please open an issue before starting work on a large feature so we can discuss the approach.
- Keep pull requests focused on a single concern.
- Ensure the project builds with no errors before submitting.

---

## License

This project is licensed under the [MIT License](LICENSE).
