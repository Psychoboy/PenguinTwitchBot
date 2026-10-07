# Global Variables

Global Variables provide persistent key-value storage accessible across actions, sub-actions, and scripts.

---

## Purpose & Usage

When building complex multi-step workflows, you often need to store information across separate events:
- **Stream State**: Storing whether a boss fight or giveaway is currently active.
- **Counters & Goals**: Tracking community goals, stream milestones, or custom tallies.
- **Viewer-Assisted Variables**: Saving usernames of current champions, VIP of the day, or recent challenge winners.
- **Dynamic Content**: Storing dynamic links or strings updated during broadcast.

---

## Global Variables vs. Local Action Variables

It is important to understand the two types of variables in Penguin Twitch Bot:

1. **Global Variables**:
   - Persistent across bot restarts and stored in the database.
   - Configured on this page or modified via external scripts and subactions.
   - Not injected automatically into action templates: load them into a local action variable using the `Get Global Variable` subaction before template use.

2. **Local Action Variables**:
   - Scoped specifically to an action run during execution.
   - Created dynamically by Triggers (e.g., `%User%`, `%Args%`, `%Bits%`), caller actions (`Execute Action`), and sub-actions (e.g., `Set Variable`, `MultiCounter`, `RandomInt`, `CheckPoints`).
   - Cleared automatically once the action finishes execution.

---

## Managing Global Variables

- **Add Variable**: Click **Add Variable** in the top toolbar to create a new key and initial value.
- **Inline Editing**: Click the Edit icon on any variable row to quickly modify its current string value.
- **Search & Filter**: Use the search field to filter by variable name when managing large variable lists.
- **Available Variables Reference**: On any Action edit page, you can open the **Available Variables** reference to view all configured global variable names to easily reference them when setting up `Get Global Variable`.

---

## Sub-Action Integration

- **Get Global Variable**: Retrieves a persistent global variable and stores its value into a local action variable name.
- **Set Variable**: Assigns a local variable value for the current action execution sequence.
- **Template Replacement**: Use `%variableName%` in chat messages, OBS text fields, TTS announcements, and API payloads to inject values dynamically.
