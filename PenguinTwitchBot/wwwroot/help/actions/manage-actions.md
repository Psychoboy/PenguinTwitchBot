# Action Management

Actions are the core automation engine of Penguin Twitch Bot. An action is a named workflow composed of one or more **triggers** (which determine when the action runs) and a sequence of **sub-actions** (which define what the bot does when triggered).

---

## Key Features

- **Multi-Trigger Execution**: A single action can be triggered by multiple distinct events (chat commands, channel point redemptions, follows, raids, timers, or hotkeys).

Use **Actions > Create from recipe > Points reward or command** to add a Manage Points sub-action with a point type, operation, amount or `$math(...)$` formula, optional chat message, and command or event trigger.

Use **Message sequence** to send up to five chat messages from one action, with a configurable delay before each message after the first.

Use **Counter command** with an existing counter to create an increment command and display the updated value. Create the counter first in Actions > Counters.

Use **Timed announcements** to create a repeating timer group with an interval, minimum chat activity, and up to five message variants. Each timer run chooses one message action.

Use **Timed raffle** to create viewer-join and moderator-start commands, periodic announcements, winner settings, and automatic closing. The start action occupies the Raffles queue for the configured duration; viewer entries remain on the shared Default queue.
- **Sequential Sub-Action Pipeline**: Sub-actions execute in order from top to bottom, with support for delays, logic conditions, and variable passing.
- **Context-Aware Available Variables**: View and search all available template variables directly within the action and subaction dialogs with click-to-copy tokens.
- **Error Catch Handlers**: Dedicated **Catch SubActions** block that triggers automatically if an error occurs during execution, providing the `%ActionErrorMessage%` variable.
- **Queue Assignment**: Direct actions into dedicated queues (such as blocking audio queues or concurrent background tasks).
- **Import & Export**: Share action setups or back them up as JSON.
- **Import & Export**: Import user-provided JSON action packages or export actions for backup and sharing. Recipes provide guided setup for common workflows.

---

## Action Properties

When creating or editing an action, configure the following:

- **Name**: A descriptive label for the action (e.g., `Raid Welcome`, `Hydrate Reward`).
- **Group / Category**: Organizational grouping to keep related actions tidy.
- **Queue**: Which execution queue handles this action. For actions with sound or screen alerts, a blocking queue ensures they do not play over each other.
- **Enabled**: Toggles whether the action will execute when triggered.
- **Concurrent Execution**: When enabled, all sub-actions within this action execute simultaneously (at the same time in parallel) rather than sequentially one after another.

---

## Managing Triggers and Sub-Actions

1. Select an action from the list on the left to load its details into the editor panel.
2. In the **Triggers** section, click **Add Trigger** to bind chat commands, Twitch events (raid, cheer, sub, follow), channel points, or hotkeys.
3. In the **Sub-Actions** section:
   - Click **Available Variables** to inspect all variables available to the action (including triggers, caller actions, preceding subactions, and globals).
   - Click **Add SubAction** to add steps such as sending chat messages, changing OBS scenes, playing sounds, triggering overlay widgets, or adjusting viewer points.
   - When configuring any subaction in Step 3, expand the **Available Variables** drawer to search and copy tokens like `%User%`, `%Args%`, or `%TargetUser%`.
4. Reorder sub-actions using the up/down arrows to adjust execution flow. Preceding subactions pass their variables forward to downstream steps.
5. In the **Catch SubActions** section, configure fallback steps to run if an error occurs (such as notifying chat or logging).
6. Click **Test** in the toolbar to execute the action immediately and verify your setup.
