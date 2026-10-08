### Twitch Developer Application & Extension Configuration

Configure developer credentials and extension secrets so Penguin Bot can connect to Twitch APIs and securely verify Twitch Extension requests.

---

#### 1. Twitch Developer Application Setup

To connect Penguin Bot to Twitch, you must register an application in the [Twitch Developer Console](https://dev.twitch.tv/console/apps):

1. **Log in** with your Twitch broadcaster account at the Twitch Developer Console.
2. Click **Register Your Application**.
3. Set **Name** to your preferred application name (e.g. `MyChannelPenguinBot`).
4. Set **OAuth Redirect URLs** to your bot's base URL callback endpoints:
   * Local: `https://localhost:5001/redirect` and `https://localhost:5001/signin` (or HTTP equivalents: `http://localhost:5000/redirect`, `http://localhost:5000/signin`).
   * Remote/Production: `https://yourdomain.com/redirect` and `https://yourdomain.com/signin`.
5. Set **Category** to *Chat Bot* or *Website Integration*.
6. Click **Create**.
7. Copy the **Client ID** and click **New Secret** to generate your **Client Secret**.

---

#### 2. Configuring Application Credentials

Enter the credentials in Penguin Bot under **Bot & Streamer Authentication**:

* **Broadcaster Channel**: The Twitch login name of your streaming channel (lowercase).
* **Bot Username**: The Twitch login name of the account used to post chat messages.
* **Twitch Client ID**: The Client ID from your Twitch Developer Application.
* **Twitch Client Secret**: The Client Secret generated in your Twitch Developer Application.

> [!TIP]
> **Saving Credentials:**
> Saving these credentials updates `appsettings.secrets.json`. If you change your Client ID or Client Secret, re-authenticate your Streamer and Bot accounts using the buttons above.

---

#### 3. Twitch Extension Secret (Advanced)

If you use a custom Twitch Extension for overlays or interactive panels:

1. Navigate to your extension in the [Twitch Developer Console](https://dev.twitch.tv/console/extensions).
2. Go to **Extension Configuration** or **Extension Details** to find the **Shared Secret**.
3. Copy the 256-bit **Base64-encoded secret** string.
4. Paste it into the **Twitch Extension Secret** field and click **Save Configuration**.

> [!NOTE]
> **Security & Hot-Reload:**
> Penguin Bot uses this secret to verify JWT signatures on incoming `/api/extension/*` requests. The secret is saved to `appsettings.secrets.json` and hot-reloaded immediately without restarting Penguin Bot.

