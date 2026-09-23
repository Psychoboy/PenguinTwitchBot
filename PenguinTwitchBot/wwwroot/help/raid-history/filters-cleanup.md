# Filtering & History Cleanup

Keep your raid history organized, actionable, and free from outdated entries.

---

### Search & Filtering

- **Creator / Game Search**: Use the search box above the table to search creators by their Twitch username, display name, or the game they were playing.
- **Online Only**: Toggle the **Online only** checkbox to immediately narrow down the list to creators currently broadcasting live. This is ideal when choosing who to raid at the end of your stream.
- **Column Sorting**: By default, the table is sorted by **Last In.** descending to immediately highlight recent raiders. You can click any column header (such as *Name*, *Game*, *Incoming*, *Avg In.*, *Last In.*, *Outgoing*, *Avg Out.*, or *Last Out.*) to sort ascending or descending. MudTable retains the active sort column as you navigate.

---

### Cleaning Up Old Records

Over time, your raid database may accumulate creators who no longer stream or channels from one-off interactions:

1. **Individual Removal**:
   - Click the **Remove** button in the *Actions* column to delete a single creator's entry from your history.
2. **Bulk Pruning (Clean Up History)**:
   - Click the **Clean Up** button in the toolbar.
   - Choose an inactivity cutoff period:
     - Inactive > **30 days**
     - Inactive > **90 days**
     - Inactive > **180 days**
     - Inactive > **1 year**
   - **Date Basis**: Pruning evaluates the **latest interaction date** (the most recent timestamp between *Last Incoming Raid* and *Last Outgoing Raid*). A creator is only removed if their most recent raid interaction on either side is older than the chosen cutoff. If a creator raided you 60 days ago but you raided them 10 days ago, they will **not** be pruned under a 30-day cutoff.

