# What Gets Checked

Everything the validator looks at, grouped by the categories you see on the [Validation Status](/validation-status) page.

---

### Actions

| Check | Severity | What it means |
| :--- | :--- | :--- |
| Missing name or queue name | Error | The action has no name, or no queue assigned. |
| No sub-actions | Warning | The action has nothing to do when it runs. |
| Duplicate action name | Warning | Two actions share a name, which makes them hard to tell apart. |
| Circular dependency | Error | Action A runs B, which runs A again. Loops that count down with a variable and exit are allowed. |
| Queue missing or disabled | Warning | The action uses a queue that is not configured on the [Queues](/actions/queues) page, or that is turned off. |
| Feature turned off | Warning | A trigger or sub-action needs a feature (Fishing, TTS, minigames and so on) that is disabled, so it will not run. |
| Runs a disabled action | Warning | An Execute Action or For Each Viewer points at an action that is disabled. |
| OBS connection problem | Error / Warning | An OBS sub-action has no connection selected or the connection no longer exists (error), or the connection is disabled (warning). |
| Empty chat message | Warning | A Send Message or Reply sub-action has no text. |

Actions with no triggers are not reported.

---

### Triggers

| Check | Severity | What it means |
| :--- | :--- | :--- |
| Orphaned trigger | Error | The trigger has no action, or its action no longer exists. |
| Missing or broken reference | Error | A command, keyword or timer trigger does not point at an existing command, keyword or timer group. |
| Duplicate trigger | Warning | The same trigger was added to one action more than once. |
| Invalid Twitch event configuration | Error | The event trigger is missing its event name or has unreadable settings. |

Command, keyword and timer triggers find their command, keyword or timer group by its **ID**, not by the name shown in the list. That name is just a label, kept up to date automatically if you rename the command, keyword or timer group.

---

### Sub-actions

Sub-actions are checked for the things they depend on: commands, actions, default commands, point types and timer groups that must exist, valid delays and URLs, file paths, OBS scene and filter names, and complete If/Else conditions.

---

### Commands and Keywords

| Check | Severity |
| :--- | :--- |
| Missing name | Error |
| Two action commands with the same name | Error |
| Action command named the same as a built-in command | Warning |
| Command maximum cooldown lower than its minimum | Warning |
| Action triggered by a disabled command | Warning |
| Keyword that is not a valid regular expression | Error |
| Two keywords with the same pattern | Warning |
| Command or keyword that no action uses | Note |

---

### Timers

| Check | Severity |
| :--- | :--- |
| Minimum interval longer than the maximum | Error |
| Timer group is inactive but still has actions | Warning |
| Timer group is active but has no enabled actions | Note |

---

### Settings and Integrations

| Check | Severity |
| :--- | :--- |
| A game's point type is missing or was deleted (see [Game Settings](/settings/games)) | Error |
| [Raid Rewards](/settings/raidrewards) is on but its point type is missing | Error |
| An enabled [OBS connection](/obs/connections) is not connected | Warning |
