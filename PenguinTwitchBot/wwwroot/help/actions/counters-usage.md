# Chat Commands & Sub-Actions

Counters integrate seamlessly with Twitch chat commands, action triggers, and variable substitutions.

---

## Chat Command Syntax

When a counter is linked to a command using the `CommandArgs` sub-action operation, viewers and moderators can interact with it using intuitive arguments:

- **`!counter`** (no arguments): Queries and prints the current value without modifying it (accessible to everyone).
- **`!counter +`** / **`!counter inc`**: Increases the counter by the configured step amount.
- **`!counter -`** / **`!counter dec`**: Decreases the counter by the configured step amount.
- **`!counter +5`** / **`!counter -3`**: Relative adjustment by a specified positive or negative number.
- **`!counter 42`** / **`!counter set 42`**: Explicitly sets the counter to the specified number.
- **`!counter reset`**: Resets the counter back to its configured initial value.

> Each operation checks the user's role against the counter's configured rank permissions.

---

## Multi-Counter Sub-Action

In the **Manage Actions** editor, add the **Multi-Counter** sub-action to automate counter changes:

- **Counter**: Select the target counter to modify.
- **Operation**:
  - **Increment**: Adds the configured step (or custom amount) to the counter.
  - **Decrement**: Subtracts the configured step (or custom amount) from the counter.
  - **Set**: Changes the counter directly to a specified target value.
  - **Reset**: Reverts the counter to its initial value.
  - **CommandArgs**: Evaluates the incoming chat command parameters dynamically at runtime (interpreting `+`, `-`, numbers, or `reset`).
- **Custom Amount**: Optional numeric override when incrementing, decrementing, or setting.

---

## Variable Substitutions

Use counter variables in chat messages, Discord webhooks, OBS text sources, or other sub-actions:

- **`%counter_value%`**: The current value of the counter modified by the current sub-action.
- **`%counter_<name>%`**: Retrieves the current value of any specific counter by name (e.g., `%counter_deaths%`, `%counter_wins%`).
- **`%counter_display_name%`**: The display name of the counter.
- **`%counter_previous_value%`**: The value of the counter prior to the latest modification.
