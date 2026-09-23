# Application Updates & Versions

The Updates tool automatically tracks new releases of Penguin Twitch Bot published on GitHub and provides automated single-click updating and rollback capabilities.

---

### Checking for Updates

- The page displays your **Current Version** (e.g. `v1.2.0`) and target **Runtime Identifier (RID)** (e.g. `linux-x64`, `win-x64`).
- Click **Check For Updates** to query the GitHub releases API.
- If an update is detected, the status chip turns yellow with **Update Available** and the **Update Now** button becomes active.

---

### Automated In-App Update Process

When you click **Update Now**:

1. **Download**: The updater downloads the release asset archive matching your specific operating system and architecture.
2. **Recovery Bundle**: A snapshot of the currently running application binary and configuration is saved into a recovery bundle.
3. **Execution**: The updater bootstrap process stops the bot, extracts and overwrites application files, and restarts the updated bot service.
4. **Progress Feedback**: Real-time progress percentage and status messages are displayed in the dashboard during the operation.

> [!IMPORTANT]
> Do not force restart your server or kill the process while an update is in progress. The bot will automatically reload when the new binary is in place.

