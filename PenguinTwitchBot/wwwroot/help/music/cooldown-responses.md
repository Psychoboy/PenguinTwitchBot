# Cooldown Chat Responses & Automation

Configure how the bot informs viewers when a requested song is currently on cooldown.

---

### Chat Response Template

- **Send Chat Response Message on Cooldown**: Toggle to send a public chat notification when a viewer requests a track on cooldown.
- **Message Template**: Customize the message text.
  - `{0}`: Replaced by the Song Title (or Video ID if title is unavailable).
  - `{1}`: Replaced by the remaining time on cooldown (e.g. `42 minutes`, `1 hour 15 minutes`).
  
#### Example Template
```text
Sorry, "{0}" was played recently! It will be available again in {1}.
```

---

### Integration with Sub-Actions & Triggers

- **Action Sub-Action: `ResetCooldowns`**:
  - The `ResetCooldowns` sub-action handler allows you to automatically clear all song cooldowns on specific stream events (such as stream start, a raid, or a specific reward redemption).
- **Exemption Handling**:
  - When songs are skipped or vetoed, the bot's internal event pipeline immediately invokes cooldown clearance when the exemption flag is active.

