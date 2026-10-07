# Twitch Extension Setup & Self-Hosting Guide

This guide walks you through registering, configuring, and running the **Penguin Bot Panel Extension** for Twitch.

---

> [!WARNING]
> ### Crucial Notice: `localhost` Will NOT Work for Other Viewers
> Setting the Bot URL to `https://localhost:8080/` only works for **your personal browser on the same computer where the bot is running**.
> 
> When real viewers on Twitch open your channel, `localhost` from their device points to **their own machine**, not your bot! 
> 
> **To allow viewers to use the extension, your bot MUST be reachable via a public, secure HTTPS URL.**

---

## Architecture Overview

1. **Frontend (Panel & Config UI)**:
   - Built with lightweight HTML, CSS, and Vanilla JavaScript (`wwwroot/extension/`).
   - Hosted on Twitch's global CDN in "Hosted Test" mode (or tested locally).
   - Embedded in the streamer's channel page as a panel (fixed 318px width).
2. **Backend (PenguinBot EBS)**:
   - Runs ASP.NET Core Kestrel serving REST endpoints on `/api/twitch-extension/*`.
   - Validates Twitch JWT identity tokens using your Extension Secret (HMAC-SHA256).
   - Features dual-layer gating: tabs and endpoints are only active if enabled both in the bot's runtime coordinator and in the broadcaster's extension config.

---

## Part 1: Connecting the Bot to the Internet (Recommended: Public HTTPS)

> [!TIP]
> **Highly Recommended**: Use a free Cloudflare Tunnel (`cloudflared`) or reverse proxy.
> This completely bypasses local certificate installation, browser security flags, and router port-forwarding, while providing a globally trusted DigiCert/Let's Encrypt SSL certificate that works on every browser and mobile device.

### Method A: Free Cloudflare Tunnel (Easiest & Fastest, Zero Cert Setup)
1. Open a terminal or command prompt and run:
   ```bash
   npx cloudflared tunnel --url http://localhost:5000
   ```
   *(Or download the standalone `cloudflared` binary from Cloudflare).*
2. Cloudflare will output an official public HTTPS URL, for example:
   ```text
   https://random-words-1234.trycloudflare.com
   ```
3. Use this URL as your **Bot API Base URL** in Twitch Extension settings. That's it! No router configuration or certificate installation required.

### Method B: Custom Domain or Reverse Proxy (Nginx, Caddy, Cloudflare DNS)
If you run Penguin Bot on a VPS or home server with a domain name (e.g. `https://bot.yourdomain.com`):
1. Configure Nginx, Caddy, or Traefik with Let's Encrypt SSL reverse-proxying to port `5000`.
2. Enter `https://bot.yourdomain.com` as your **Bot API Base URL**.

---

## Part 2: Twitch Developer Console Registration

1. Go to the [Twitch Developer Console](https://dev.twitch.tv/console/extensions).
2. Click **Create Extension**.
   - **Name**: e.g., `Penguin Bot` (or your preferred name).
   - **Type of Extension**: Select **Panel**.
   - **Summary & Description**: Fill in a brief description.

### 1. General Settings & Capabilities
Under your Extension Version's **General / Capabilities** settings:

- **Request Identity Link**:
  - Set to **"Yes, I would like my extension to request an identity link."**
  - *Why*: Allows viewers to share their Twitch ID with the extension with 1 click so their giveaway entries, points, and fishing inventory bind to their Twitch account.
- **Privacy Policy URL**:
  - Link to your hosted privacy policy markdown or webpage (a ready-to-use template is provided at [privacy-policy.md](privacy-policy.md)).
- **Chat Capabilities**:
  - Set to **No** (unless you want the extension to send chat messages via Twitch API).

### 2. Extension Configuration Service
Under **Select how you will configure your extension**:
- Select **"Extension Configuration Service"**.
- Leave **Developer Writable Channel Segment Version** and **Broadcaster Writable Channel Segment Version** **BLANK**.
  - *Why*: This enables Twitch's hosted configuration store (`window.twitch.ext.configuration`). Broadcasters can configure their bot URL, tab ordering, and feature toggles inside Twitch Creator Dashboard without requiring an external database.

### 3. Allowed Domains (Content Security Policy / CSP)
Twitch strictly enforces Content Security Policy (CSP) headers on extensions. Any domain not explicitly allowlisted here will be blocked by the browser:

1. **Allowlist panel URLs**:
   - Comma-separated list of URLs that your extension's viewer panel is allowed to open via links (`target="_blank"`) or navigation (such as the "Open Full Web Interface" link or "View Full Giveaway Details & Rules" button):
     ```text
     https://twitch.tv/, https://dev.twitch.tv/, https://*.trycloudflare.com/, https://localhost:8080/
     ```
     *(If using a custom domain like `https://bot.yourdomain.com/`, add it here).*

2. **Allowlist for Image Domains** (CSP `img-src`):
   - Comma-separated list of base URLs from which your extension is allowed to load images (e.g. `<img>` tags for giveaway prize images, custom icons, or external emblems):
     ```text
     https://twitch.tv/, https://dev.twitch.tv/, https://*.trycloudflare.com/, https://i.imgur.com/, https://imgur.com/, https://localhost:8080/
     ```
     *(Be sure to include any image host you use to host giveaway images, including your bot domain).*

3. **Allowlist for URL Fetching Domains** (CSP `connect-src`):
   - Comma-separated list of base URLs where your extension makes API calls (`fetch`):
     ```text
     https://twitch.tv/, https://dev.twitch.tv/, https://*.trycloudflare.com/, https://localhost:8080/
     ```
     *(Add your custom domain here if using Method B).*

- Scroll down and click **Save Changes**.

### 4. Extension Views & Asset Hosting
Under **Asset Hosting / Extension Views**:
- **Panel Viewer Path**: `panel.html`
- **Broadcaster Config Path**: `config.html`
- **Testing Base URI** (if using Local Test):
  ```text
  https://localhost:8080/extension/
  ```
- **If using Hosted Test (Recommended for Release)**:
  - Zip the contents of `PenguinTwitchBot/wwwroot/extension/`:
    - `panel.html`
    - `config.html`
    - `css/`
    - `js/`
    - *(Note: A pre-packaged clean zip is generated at `PenguinTwitchBot/extension.zip`)*.
  - Upload the zip to Twitch Asset Hosting.

### 5. Access & Streamer Allowlist (Restricting to Your Channel)
Under the **Access** tab:

> [!IMPORTANT]
> **Restrict Streamer Allowlist to Just Your Stream**:
> - **Streamer Allowlist**: Search for and add **only your streamer account** (e.g. `SuperPenguinTV`).
>   - *Why*: If this field is left empty or missing, **all broadcasters across Twitch** can see and install your extension from the Extension Manager! Because this extension is designed to interface with your personal, self-hosted Penguin Bot backend, you should restrict installation access exclusively to your own channel.
> - **Testing Account Allowlist**: Add your streamer account and any moderator or alt accounts (e.g. `SuperPenguinTV`, `Super_Waffle_...`) that need access to test the extension in test mode without public test disclosure banners.
> - Click **Save Changes**.

---

## Part 3: Understanding Secrets & Bot Configuration

There is an important distinction between Twitch's **API Client Secret** and the **Extension Client Secret**:

### 1. Twitch API Client Secret (For the Bot Chat & Helix API)
- **Where to find it**: Under [Twitch Applications Console](https://dev.twitch.tv/console/apps).
- **Behavior**: Twitch **only displays this secret once** upon creation. If you lose it or close the window, you must click *New Secret* to regenerate it.
- **Used for**: Connecting the bot to Twitch chat, managing channel points, EventSub, and Helix API requests.
- **Config location**: `"Twitch": { "ClientSecret": "..." }`.

### 2. Extension Client Secrets / Shared Secret (For the Extension Panel)
- **Where to find it**: Under [Twitch Extensions Console](https://dev.twitch.tv/console/extensions) ➔ Click your extension ➔ **Extension Settings**.
- **Behavior**: You can create **multiple active Extension Secrets** (supporting zero-downtime key rotation), and you **can view and retrieve them again later** from the Extension Settings list.
- **Used for**: Cryptographically verifying the viewer's signed JWT token (identity, channel ID, and role) sent by the panel extension when making requests to `/api/twitch-extension/*`.
- **Config location**: `"Twitch": { "Extension": { "Secret": "..." } }` (or `"TwitchExtension:Secret"`).
- **Note on Client ID**: The **Extension Client ID is NOT required** by the bot! The backend only needs the Base64 Extension Secret to validate HMAC-SHA256 token signatures.

---

### Configuring `appsettings.json` (or `secrets.json`)

```json
{
  "Twitch": {
    "ClientId": "your-bot-application-client-id",
    "ClientSecret": "your-bot-application-secret",
    "Extension": {
      "Secret": "your-base64-extension-secret"
    }
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      },
      "Https": {
        "Url": "https://0.0.0.0:8080"
      }
    }
  }
}
```

---

## Part 4: Broadcaster Activation & Setup

Once the extension is in **Hosted Test** (or Local Test):

1. Go to your [Twitch Creator Dashboard](https://dashboard.twitch.tv/extensions).
2. Go to **Extensions** ➔ **My Extensions**.
3. Find your extension under **In Test** (or Installed).
4. Click **Configure** (the gear icon):
   - **Bot API Base URL**: Enter your public HTTPS address (e.g. `https://your-tunnel.trycloudflare.com` or `https://bot.yourdomain.com`).
   - **Reorder Tabs**: Drag or use Up/Down arrows to prioritize tabs (Default: Giveaway ➔ Fishing ➔ Rankings ➔ Commands).
   - **Feature Toggles**: Enable or disable specific tabs.
   - Click **Save Extension Settings**.
5. Click **Activate** ➔ Set as **Panel 1**.
6. Visit your Twitch channel page (`twitch.tv/yourchannel`) under the **About** tab to view your live panel!

---

## Part 5: Extension Review & Submission Checklist (Getting Approved)

Before moving your extension from **02 Hosted Test** through **03 In Review** to **04 Approved** and **05 Released**, Twitch requires all metadata and configuration items to be completely and accurately filled out. Incomplete submissions will be rejected by Twitch review moderators.

Use this checklist to ensure all required fields are satisfied:

| Category | Item | Required Value / Recommendation |
| :--- | :--- | :--- |
| **Version Details** | **Name** | `Penguin Bot` (must match branding in your panel and description). |
| | **Summary** | Clear 1-sentence summary (e.g., *Interactive panel extension for giveaways, fishing minigames, leaderboards, and chat commands.*). |
| | **Description** | Detailed summary covering what viewers can do (view giveaways & enter tickets, participate in the fishing game, equip gear, inspect channel leaderboards, and look up chat commands). |
| | **Category** | Select **Streamer Tools** or **Viewer Engagement**. |
| | **Support Email** | A valid contact email monitored by the streamer. |
| **Access Control** | **Streamer Allowlist** | Add **only your streamer Twitch account** (e.g. `SuperPenguinTV`) to prevent other channels from installing your private self-hosted bot extension. |
| | **Testing Account Allowlist** | Add your streamer username and any testing/moderator accounts. |
| **Capabilities** | **Request Identity Link** | **Yes** (required for account-bound giveaway tickets, fishing inventory, and user rankings). |
| | **Privacy Policy URL** | Direct URL to your public privacy policy webpage or hosted markdown file (see [privacy-policy.md](privacy-policy.md)). |
| | **Chat Capabilities** | Set to **No** (the extension operates via panel UI and REST endpoints). |
| **Monetization** | **Bits in Extensions** | Select **No** (unless you have separately configured Twitch Bits product catalogs). |
| **Asset Hosting** | **Archive Package** | Upload `PenguinTwitchBot/extension.zip`. Verify file structure contains `panel.html`, `config.html`, `css/`, and `js/` at the root. |
| | **Viewer Path** | `panel.html` |
| | **Config Path** | `config.html` |
| **Allowed Domains (CSP)** | **Connect-Src (URL Fetching)** | Add your public HTTPS endpoint (e.g., `https://*.trycloudflare.com/` or your custom domain). Requests to unlisted domains will be blocked by Twitch CSP. |
| | **Image Domains (`img-src`)** | Add base URLs from which the extension loads images (e.g. `https://*.trycloudflare.com/`, `https://i.imgur.com/`, or your custom domain where prize images are stored). |
| | **Panel URLs** | Add base URLs that the viewer page is allowed to open in a new tab (e.g. `https://*.trycloudflare.com/` or your bot domain for the web dashboard and giveaway details links). |
| **Visual Assets** | **Icons** | Upload required extension icon sizes (100x100 and 24x24 px). |
| | **Screenshots** | Upload at least 1 screenshot of the extension in action (1280x720 px recommended). |
| **Walkthrough & Review Notes** | **Review Notes** | In the reviewer notes field, clearly explain: (1) This extension is a self-hosted channel companion panel restricted exclusively to the channel via the Streamer Allowlist. (2) State that the bot backend is reachable at your configured public URL. (3) Provide step-by-step instructions on what reviewers can test (e.g., viewing leaderboards, browsing fish store items, and entering giveaways). |

---

## (Optional) Part 6: Localhost Developer Testing with Self-Signed Certificates

> [!NOTE]
> This section is strictly for offline local debugging where an internet tunnel is not desired. Remember that this setup will only work on your local machine.

`PenguinTwitchBot` includes an internal CA generator and persistent certificate loader:
- Root Certificate Authority: `Data/certs/ca.crt` (`CA:TRUE`)
- Server Leaf Certificate: `Data/certs/localhost.pfx` (`CA:FALSE`, signed by `ca.crt`)

### Installing `ca.crt` on Windows (Edge / Chrome)
1. Open `PenguinTwitchBot\Data\certs\` in Windows Explorer.
2. Double-click `ca.crt`.
3. Click **Install Certificate...**.
4. Select **Current User** ➔ **Next**.
5. Select **"Place all certificates in the following store"** ➔ click **Browse...**.
6. Select **Trusted Root Certification Authorities** ➔ **OK** ➔ **Next** ➔ **Finish**.
7. Confirm **Yes** on the Windows security prompt.

### Installing `ca.crt` in Firefox
1. Open Firefox Settings (`about:preferences#privacy`).
2. Scroll down to **Certificates** ➔ click **View Certificates...**.
3. Under the **Authorities** tab, click **Import...**.
4. Select `Data/certs/ca.crt`.
5. Check **"Trust this CA to identify websites"** ➔ click **OK**.
6. If Firefox still prompts on port 8080, open `https://localhost:8080/api/twitch-extension/features` in a new tab once and click *Advanced ➔ Accept Risk and Continue*.
