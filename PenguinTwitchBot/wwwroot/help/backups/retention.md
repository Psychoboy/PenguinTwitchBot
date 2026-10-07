# Backup Retention Policies

Automated retention settings control how many backup files are maintained and prevent disk storage exhaustion over time.

---

### Retention Constraints

When automated backups run or a new backup is created, the system applies two simultaneous constraints to clean up older archives:

1. **Max Backups to Keep**:
   - Caps the absolute number of backup `.zip` files stored on disk (e.g. keep `15` most recent).
   - If a new backup exceeds this limit, the oldest backup is deleted.
2. **Max Days to Keep**:
   - Backups older than this age in days (e.g. `30` days) are automatically deleted, even if total backup count is under the maximum.
3. **Chat History Months to Include**:
   - Chat logs can grow very large in active channels. This setting restricts the backup to only include chat history from the last `N` months (e.g. `12` months), significantly reducing backup file size and speeding up archive creation.

---

### Saving & Resetting Settings

- Adjust retention numbers as needed and click **Save Settings**. Changes take effect immediately on subsequent backup operations.
- Click **Reset to Defaults** to restore standard recommended retention values (`15` backups, `15` days, `12` months chat history).

