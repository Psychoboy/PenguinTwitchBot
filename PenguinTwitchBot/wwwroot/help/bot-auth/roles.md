### Streamer Account vs. Bot Account

Penguin Twitch Bot uses two distinct Twitch authentication connections to interact with Twitch:

#### Streamer (Broadcaster) Account
* **Role**: Channel owner & administrator.
* **Capabilities**: Authorizes broadcaster-level EventSub WebSocket events, custom channel point rewards, stream titles/game categories, raids, and subscriber tracking.
* **Permissions**: Requires broadcaster authorization via Twitch OAuth.

#### Bot Account
* **Role**: Chat actor & announcement speaker.
* **Capabilities**: Reads and posts messages into chat, responds to viewer commands, triggers timed announcements, and executes shoutouts.
* **Identity**: Can be a dedicated bot Twitch account (e.g. `YourChannelBot`) or your main streamer account.

> [!TIP]
> **Dedicated Bot Account Recommendation:**
> While you can authenticate your main streamer account for both roles, creating a separate Twitch account gives chat a clean, branded bot identity and keeps your streamer chat logs tidy.

