# Global Variables

Global Variables provide persistent key-value storage accessible across actions, sub-actions, and scripts.

---

## Purpose & Usage

When building complex multi-step workflows, you often need to store information across separate events:
- **Stream State**: Storing whether a boss fight or giveaway is currently active.
- **Counters**: Tracking total deaths, wins, catches, or community goals.
- **Viewer-Assisted Variables**: Saving usernames of current champions, VIP of the day, or recent challenge winners.
- **Dynamic Content**: Storing dynamic links or strings updated during broadcast.

---

## Managing Variables

- **Add Variable**: Click **Add Variable** in the top toolbar to create a new key and initial value.
- **Inline Editing**: Click the Edit icon on any variable row to quickly modify its current string value.
- **Search & Filter**: Use the search field to filter by variable name when managing large variable lists.

---

## Sub-Action & Trigger Integration

Variables can be accessed and modified automatically during action runs:
- **Set Variable Sub-Action**: Updates a global variable's value dynamically (supports literal text, counter increments, or values derived from trigger parameters like `%username%`).
- **Template Insertion**: Insert stored variables into chat messages, TTS announcements, or overlay widget payloads using variable syntax (e.g. `$(variableName)` or sub-action parameter references).
