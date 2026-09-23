# Diagnostics & Resolving Issues

Step-by-step guidance for fixing issues detected by the validation engine.

---

### Fixing Missing Sound Files

When an audio file error is flagged:
1. Note the missing file path displayed in the validation result.
2. Ensure the sound file is copied into the bot's configured sounds directory (or update the file path in [Audio Commands](/actions/audiocommands) or the specific `PlaySound` sub-action).
3. Re-run validation to verify resolution.

---

### Resolving Missing Action / Connection References

- **Deleted Actions**: If an `ExecuteAction` subaction targets a deleted action, edit the parent action in [Manage Actions](/actions/manage) and update or remove the target step.
- **OBS Connections**: If an OBS connection was renamed or recreated, open the affected OBS sub-actions and reselect the appropriate connection from the dropdown.

---

### Cleaning Orphaned SubActions

Orphaned records can occasionally occur after restoring partial backups:
- Click the **Fix / Clean Up** button if available next to the rule to safely remove orphaned sub-action entries.

