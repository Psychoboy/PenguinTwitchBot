# Database Backups & Restoration

The Backup subsystem ensures that your bot configuration, actions, custom commands, viewers, point balances, giveaways, and settings are safely archived into compressed ZIP packages.

---

### Manual Backups

- Click **Create Backup** in the top-right toolbar.
- The bot serializes the database tables into structured JSON files and packages them into a timestamped `.zip` file stored in the local `backups/` directory.
- Once completed, the backup immediately appears in the **Existing Backups** table with its creation date and file size.

---

### Downloading Backups

- On any backup row in the table, click **Download**.
- Transfers the `.zip` archive directly to your local workstation.
- Highly recommended before performing major system upgrades or complex database refactoring.

---

### Restoring from Backup

1. In the **Existing Backups** table, locate the desired backup.
2. Click **Restore**.
3. Confirm the restoration prompt.
4. The system will unpack the archive and restore table records.

> [!CAUTION]
> Restoring a backup replaces current database records with the backed-up data. Any new actions or points accrued since the backup was taken will be overwritten. Always create a fresh backup before restoring an older state.

