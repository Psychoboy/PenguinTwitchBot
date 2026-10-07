# Privacy Policy for Penguin Bot Extension

**Last Updated:** October 7, 2026

This Privacy Policy describes how the **Penguin Bot** Twitch Panel Extension ("the Extension") collects, uses, and protects information when you view or interact with the Extension on Twitch.

---

## 1. Information We Collect

### A. Information Provided by Twitch
When you view a channel using the Extension, Twitch provides the Extension with cryptographic tokens (JSON Web Tokens / JWT) via the Twitch Extension Helper library:
- **Opaque User ID**: An anonymous identifier assigned by Twitch that distinguishes unique viewers without revealing your personal identity.
- **Channel ID**: The Twitch channel ID of the broadcaster you are watching.
- **Viewer Role**: Your role in the channel (e.g., broadcaster, moderator, viewer).

### B. Shared Twitch Identity (Identity Link)
If you choose to click **"Link Twitch Account"** (or consent to Twitch's identity-sharing prompt):
- **Twitch User ID & Username**: Your public Twitch User ID and Display Name are shared with the broadcaster's bot backend.
- *Note:* Identity sharing is completely optional. If you decline, you may still browse public standings, store items, and command listings. Linking your identity is only required to associate personal points, enter giveaways, and track personal fishing inventories.

### C. In-Extension Interaction Data
When you interact with the Extension, the following game and loyalty data is recorded by the broadcaster's self-hosted bot:
- **Loyalty Points & Tickets**: Balances earned via watch time or channel activities.
- **Giveaway Entries**: Ticket amounts and entry timestamps for active giveaways.
- **Fishing Minigame Data**: Catches, equipped rods/baits, durability, and shop purchases.

---

## 2. How Your Information Is Used

We use the collected information exclusively to provide real-time interactive stream features:
- Displaying your current channel point balance and username in the panel header.
- Managing and verifying ticket entries in channel giveaways.
- Managing your personal fishing inventory, equip/unequip states, and shop transactions.
- Displaying public rankings on channel leaderboards (points, watch time, loudest chatters).

**We do NOT:**
- Sell, rent, trade, or monetize your personal information.
- Use your data for advertising, cross-site tracking, or marketing purposes.
- Access your private Twitch messages, payment information, or account credentials.

---

## 3. Data Storage & Self-Hosting Architecture

The Extension operates on a **decentralized, self-hosted architecture**:
- The frontend panel is hosted by Twitch CDN (`ext-twitch.tv`).
- All backend data (points, inventories, giveaways) is stored directly in the **broadcaster's self-hosted database** (SQLite or PostgreSQL) on the server or computer running their `Penguin Bot` instance.
- No central third-party server collects or aggregates viewer data across different broadcasters.

---

## 4. Third-Party Services

The Extension communicates solely with:
1. **Twitch Interactive, Inc.**: To load the Extension frame and verify viewer permissions via Twitch's official APIs and CDN.
2. **The Broadcaster's Bot Server**: Over secure HTTPS to fetch and update in-channel data.

---

## 5. Your Rights & Data Control

- **Revoking Access**: You can revoke identity sharing at any time by navigating to your [Twitch Connections Settings](https://www.twitch.tv/settings/connections) under "Extensions" and revoking permissions for this extension.
- **Data Deletion / Removal**: Because all data is stored locally in the broadcaster's database, you may request the deletion or reset of your in-channel points, inventory, or giveaway entries by contacting the channel broadcaster or moderators.

---

## 6. Contact & Support

If you have questions about this Privacy Policy or the Extension's data practices, please reach out via:
- **Project Repository**: [GitHub Issues](https://github.com/Psychoboy/PenguinTwitchBot/issues)
- **Broadcaster**: Contact the channel broadcaster directly on Twitch.
