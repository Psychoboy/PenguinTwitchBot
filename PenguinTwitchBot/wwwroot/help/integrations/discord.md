### Discord Bot Setup

Connect your Discord bot to publish automated stream go-live announcements, ping mention roles when streaming starts, assign live roles to viewers, and synchronize events.

#### Discord Developer Portal Walkthrough
1. Open the [Discord Developer Portal](https://discord.com/developers/applications).
2. Click **New Application** in the top right and name your bot.
3. In the left menu, select the **Bot** tab:
   * Click **Reset Token** to generate and copy your Bot Token.
   * Under **Privileged Gateway Intents**, enable **Server Members Intent**, **Message Content Intent**, and **Presence Intent**.
4. In the left menu, select **OAuth2 &rarr; URL Generator**:
   * Scopes: Select `bot` and `applications.commands`.
   * Permissions: Select *Send Messages*, *Embed Links*, *Manage Roles*, *Mention Everyone*, and *Read Message History*.
5. Copy the generated invite link, open it in a browser, and invite the bot to your Discord server.
6. In Discord Server Settings &rarr; Roles, position the bot's role **above** any roles it needs to assign (e.g. Live Member Role).
7. Paste the bot token into the Discord tab and click **Test Connection** or **Refresh from Token**.

