# Validation Status Overview

The [Validation Status](/validation-status) page checks your setup for things that will not work the way you expect: broken references, disabled features, missing connections, settings that point at something that no longer exists, and more. It never changes your data.

---

### Running a Check

- The page runs a check automatically the first time you open it after the bot starts.
- Click **Run Again** to scan again after you fix something.
- The bot also runs the same check once a day (5:00 AM by default) and writes any problems to the log. You can change that schedule in [Bot Settings](/settings/app).

---

### Reading the Results

The banner at the top tells you where you stand:

- **Some things need fixing**: at least one error.
- **Mostly good, a few things to look at**: warnings only.
- **Everything looks good**: no errors or warnings. Notes are only for information.

Below it, the four cards show how many issues are in each group. Click a card to show only that severity, and click it again to clear the filter. The "Checked" chips show how much was scanned, such as how many actions and sub-actions.

---

### Severities

- **Error (red)**: Something will not work. For example an Execute Action pointing at an action that was deleted, or an OBS sub-action with no connection.
- **Warning (orange)**: Probably not what you want, but nothing is strictly broken. For example an action that needs a feature you have turned off, or a timer group that is inactive but still has actions.
- **Note (blue)**: Good to know. Notes never count as a failure. For example a command that no action uses yet.

Actions without triggers are **not** a problem. They can still run from the API, from another action, or from the Test button.

---

### Working Through the List

Each row shows what is wrong, in plain words, and a lightbulb hint on how to fix it. Use the **Fix** button to jump to the right page.

- **Search and category filter** narrow the list to one area such as Timers or Commands.
- **Ignore** (the eye icon) hides an issue you have decided is fine. Ignored issues stay on the page when you switch on **Show ignored**, are remembered between runs, and never count towards the totals. Click the icon again to restore one.
