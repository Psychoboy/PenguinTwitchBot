# Validation Rules Catalog

Automated validation rules protect against broken stream workflows and missing assets.

---

### Core Action & Sub-Action Rules

| Rule | Severity | Condition Checked |
| :--- | :--- | :--- |
| **Orphaned SubActions** | Error | SubAction records in the database with null or invalid parent `ActionId`. |
| **Invalid SubAction Reference** | Error | `ExecuteAction` subaction targeting an action ID or action name that was deleted. |
| **OBS Connection Reference** | Error | OBS subaction targeting an OBS connection ID that does not exist in [OBS Connections](/obs/connections). |
| **Missing Sound Files** | Error | `PlaySound` subaction or Audio Command pointing to an audio file path that cannot be found on the filesystem. |
| **Invalid Twitch Event Triggers** | Warning | Trigger configured with invalid channel point reward ID or unsupported event type. |
| **Recursive Action Loops** | Warning | Action A calls Action B which calls Action A, which could cause infinite execution loops. |
| **Unlinked Actions** | Info | Action has no triggers, timers, or commands assigned to execute it. |

---

### Timer Group Rules

- **Empty Timer Groups**: Timer groups that are enabled but contain no assigned actions.
- **Interval Bounds**: Timer intervals set below minimum recommended limits (< 60 seconds).

