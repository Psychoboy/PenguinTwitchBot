# Action Keywords

The **Action Keywords** manager (`/actions/keywords`) allows streamers to execute automated actions whenever specific words, phrases, or emotes appear in general chat messages, without requiring a leading exclamation mark (`!`).

## How Keywords Differ from Commands

- **No Command Prefix**: Keywords trigger naturally when viewers type phrases in conversation (e.g. "gg", "rip", "hypers", "welcome").
- **Match Types**:
  - **Contains**: Triggers if the keyword appears anywhere within the message text.
  - **Exact Match**: Triggers only if the message matches the keyword exclusively.
  - **Starts With**: Triggers if the message begins with the keyword.
- **Cooldowns**: To prevent chat spam from firing repeated actions, keywords support strict global and user cooldowns.
- **Action Execution**: Triggers any linked Action in the Actions engine, such as playing sound effects (`PlaySound`), overlay alerts (`Alert`), or fireworks.
