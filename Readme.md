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
- [REST API & Stream Deck Integration](#rest-api--stream-deck-integration)
- [Contributing / Building From Source](#contributing--building-from-source)
- [License](#license)

---

## Features

### Chat & Viewer Engagement
- **Custom Commands** - Create, edit, and manage chat commands with cooldowns, aliases, and per-user/global limits
- **Loyalty Points** - Viewers automatically earn points for watching; points are used as currency across features
- **Ticket System** - A separate currency used for games and raffles
- **Quote System** - Save and recall memorable quotes from chat
- **Shoutout System** - Automated and manual shoutouts for raiders and friends
- **Raid Tracker** - Tracks incoming raids and raider history
- **Last Seen** - Tracks when viewers were last active in chat
- **Death Counters & Daily Counters** - Stream counters manageable from chat or the dashboard
- **Top Lists** - Display top viewers by points, watch time, and more
- **Auto Timers** - Send periodic messages to chat on a schedule
- **Markov Chain Chat** - AI-style chat generation based on your channel's chat history
- **Blacklist / Moderation** - Word and phrase blacklisting with configurable actions

### Games & Gambling
- **Fishing Game** - A full-featured fishing minigame with an inventory system, a shop, a leaderboard, multiple fish tiers, and analytics
- **Duels** - Viewer-vs-viewer point duels
- **Roulette** - Bet your points on roulette
- **Gamble** - Classic point gambling
- **Heist** - Cooperative group heist game
- **Slots** - Slot machine game
- **Steal** - Steal points from other viewers
- **Raffles** - Configurable SubAction-based raffles with custom start, join, end, winner count, and total award flows
- **Giveaways** - Run giveaways for your community
- **Wheel Spin** - Spin a configurable prize wheel
- **Defuse** - A bomb-defusing minigame
- **Free For All (FFA)** - Everyone-against-everyone game mode

### Media & Integrations
- **Song Requests** - YouTube-based song request queue; viewers can request songs with points
- **Text-to-Speech (TTS)** - Read chat messages or channel point redemptions aloud
- **OBS Integration** - Connect to OBS for scene control and alerts
- **Discord Integration** *(optional)* - Go-live announcements, live member role assignment, and chat bridge
- **Weather Commands** *(optional)* - `!weather` command powered by OpenWeatherMap
- **AI Chat Responses** *(optional)* - OpenAI-powered chat responses and automated shoutouts
- **Channel Point Redeems** - Handle Twitch channel point redemptions with custom actions
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

The setup wizard is a browser-based application that walks you through every required and optional setting. It will open a browser window automatically. If it does not, navigate to `http://localhost:5000`.

The wizard covers the following steps:

| Step | Description |
|------|-------------|
| 1 | **Welcome** - overview of what you need |
| 2 | **Bot Identity** - your bot's Twitch username and your channel name |
| 3 | **Twitch Streamer App** - Client ID and Secret from [dev.twitch.tv](https://dev.twitch.tv/console/apps/create) |
| 4 | **Twitch Bot App** - can reuse the streamer app or use a separate one |
| 5 | **Authorize Streamer Account** - OAuth authorization via your browser |
| 6 | **Authorize Bot Account** *(optional)* - OAuth authorization for your bot account; grants `user:write:chat` and `user:bot` scopes required for sending chat messages |
| 7 | **Database** - choose SQLite, MariaDB, or PostgreSQL (see [Database Support](#database-support)) |
| 8 | **YouTube API** *(optional)* - enables the song request feature |
| 9 | **Discord Integration** *(optional)* - bot token, server ID, and channel IDs |
| 10 | **Weather** *(optional)* - OpenWeatherMap API key and default location |
| 11 | **OpenAI** *(optional)* - API key for AI-powered chat features |
| 12 | **Review & Save** - review all settings (secrets masked) and write `appsettings.secrets.json` |

When the wizard finishes, it saves your configuration to `appsettings.secrets.json` in the bot's directory and shuts itself down automatically.

#### Authorizing the bot account (Step 6)

The bot account requires a **user access token** with the `user:write:chat` and `user:bot` scopes — a plain app token is not sufficient.

Step 6 provides two options:

- **Open Auth Page in Browser** — opens the Twitch authorization page directly. Twitch will show a sign-in prompt (forced by `force_verify=true`) so you can switch accounts if needed.
- **Copy Auth URL** — generates the authorization URL and displays it in a text field so you can copy and paste it into a different browser or incognito window where your bot account is already signed in.

> **Tip:** If your bot and streamer accounts are different Twitch accounts, use **Copy Auth URL** and paste it into a browser session where the bot account is logged in.

#### Creating a Twitch Developer Application

1. Go to [dev.twitch.tv/console/apps/create](https://dev.twitch.tv/console/apps/create) and log in with your **streamer** account.
2. Give the app a name (e.g. `MyBot`).
3. Add the following OAuth Redirect URLs:
   - `http://localhost:5000/streamerredirect`
   - `http://localhost:5000/botredirect`
   - `http://localhost:5000/redirect`

  > **Running on a different host or port?** Use your actual bot URL and port in the redirect URLs above.

4. Set the category to **Chat Bot** and click **Create**.
5. Click **Manage** → **New Secret** to generate your Client Secret.
6. Copy the **Client ID** and **Client Secret** into the setup wizard.

---

### Step 2: Run the Bot

Once setup is complete, run **`PenguinTwitchBot.exe`** (Windows) or `PenguinTwitchBot` (Linux/macOS).

The bot starts a web server. Open your browser to `http://localhost:5000` (or the port shown in the console) to access the **web dashboard**.

On first launch, the database will be automatically created and migrated. The bot will then connect to Twitch and begin operating.

> **Tip:** If you skipped the bot account authorization in the setup wizard, navigate to `/botsignin` in the dashboard to authorize your bot account after the bot starts.

OAuth redirects now use the URL/host/port from the request used to access the bot, so no separate URL configuration setting is required.

---

## Database Support

The bot supports three database backends. You select your preference during the setup wizard.

| Database | Notes |
|----------|-------|
| **SQLite** *(default)* | No external server required. Stores everything in a single file (`Data/PenguinTwitchBot.sqlite`). Recommended for most users. |
| **MariaDB / MySQL** | Requires a running MariaDB or MySQL server. Provide a connection string in the wizard. |
| **PostgreSQL** | Requires a running PostgreSQL server. Provide a connection string in the wizard. |

The database is created and migrated automatically on startup. You can switch databases later by editing `appsettings.secrets.json` and re-running the bot.

---

## Optional Integrations

All optional integrations can be configured during the setup wizard or added later by editing `appsettings.secrets.json`.

### YouTube / Song Requests
Requires a **YouTube Data API v3** key from [Google Cloud Console](https://console.cloud.google.com/apis/library/youtube.googleapis.com). Used to search and queue YouTube videos for the song request feature.

### Discord
Requires a **Discord bot token** from [discord.com/developers](https://discord.com/developers/applications). Enable **Server Members Intent** and **Message Content Intent** in the bot settings. Features include:
- Go-live announcements to a designated channel
- Automatic role assignment while you are live
- Role pinging on stream start

### Weather
Requires a free **OpenWeatherMap API key** from [openweathermap.org/api](https://openweathermap.org/api). Enables the `!weather` command in chat.

### OpenAI
Requires an **OpenAI API key** from [platform.openai.com](https://platform.openai.com/api-keys). Enables AI-powered chat responses and automated shoutouts. Usage is billed by token - set spend limits in your OpenAI account dashboard.

---

## Web Dashboard

The bot includes a web-based dashboard accessible at `http://localhost:5000` (default). From the dashboard you can:

- View and manage chat commands
- Monitor viewer loyalty points and tickets
- Manage the fishing game (items, shop, leaderboards)
- Configure auto timers, shoutouts, and alerts
- View chat history and metrics
- Manage the song request queue
- Configure channel point redeems
- Run and manage giveaways
- View and manage clips
- Configure Discord, TTS, and other integrations
- Access administrative tools

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

| Overlay | URL | Description |
|---------|-----|-------------|
| **Alerts** | `http://localhost:5000/alerts.html` | Displays follow, sub, raid, cheer, and channel point redemption alerts with images, sounds, and text animations. |
| **Clips** | `http://localhost:5000/clips.html` | Plays back Twitch clips downloaded by the bot. Shows the clip video along with the clipper's name and game info. |
| **Fishing** | `http://localhost:5000/fishing.html` | Shows a real-time fishing game overlay - displays catch notifications, fish details, and leaderboard updates as viewers fish in chat. |
| **Wheel Spin** | `http://localhost:5000/wheel.html` | Renders the interactive prize wheel. Animates and displays the result whenever a wheel spin is triggered. |

### Recommended OBS Settings per Source

| Overlay | Width | Height | Notes |
|---------|-------|--------|-------|
| Alerts | 1920 | 1080 | Transparent background - place on top of other sources. |
| Clips | 1920 | 1080 | Has its own background; layer accordingly. |
| Fishing | 1920 | 1080 | Transparent background - place on top of other sources. |
| Wheel Spin | 800 | 800 | Resize to taste; no transparency by default. |

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
| `PenguinTwitchBot/` | Main bot and web dashboard |
| `PenguinTwitchBot.Setup/` | First-time setup wizard |
| `PenguinTwitchBot.Migrations.MariaDb/` | EF Core migrations for MariaDB/MySQL |
| `PenguinTwitchBot.Migrations.Postgres/` | EF Core migrations for PostgreSQL |
| `PenguinTwitchBot.Test/` | Unit tests |

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
