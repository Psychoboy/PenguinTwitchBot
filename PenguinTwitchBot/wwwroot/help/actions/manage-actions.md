# Action Management

Actions are the core automation engine of Penguin Twitch Bot. An action is a named workflow composed of one or more **triggers** (which determine when the action runs) and a sequence of **sub-actions** (which define what the bot does when triggered).

---

## Key Features

- **Multi-Trigger Execution**: A single action can be triggered by multiple distinct events (chat commands, channel point redemptions, follows, raids, timers, or hotkeys).
- **Sequential Sub-Action Pipeline**: Sub-actions execute in order from top to bottom, with support for delays, logic conditions, and variable passing.
- **Queue Assignment**: Direct actions into dedicated queues (such as blocking audio queues or concurrent background tasks).
- **Import & Export**: Share action setups or back them up as JSON.
- **Sample Actions Library**: Quickly import prebuilt actions such as Fireworks alerts, Fishing mini-games, and Wheel spin triggers.

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
2. In the **Triggers** tab, click **Add Trigger** to bind chat commands, Twitch events (raid, cheer, sub, follow), channel points, or hotkeys.
3. In the **Sub-Actions** tab, add steps such as sending chat messages, changing OBS scenes, playing sounds, triggering overlay widgets, or adjusting viewer points.
4. Drag and drop sub-actions to reorder their execution flow.
5. Click **Test Action** in the toolbar to execute the action immediately and verify your setup.

