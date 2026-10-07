# Command Cooldowns

The [Command Cooldowns](/commands/cooldowns) dashboard provides real-time visibility into all active command cooldowns across your channel.

---

## Active Cooldown Tracking

When viewers or moderators trigger commands that have user or global cooldowns configured, active cooldown timers are tracked here:

- **Command**: Name of the command currently locked on cooldown.
- **Scope / User**:
  - `Global`: Indicates the entire channel is waiting for the global cooldown timer to expire before the command can be used again.
  - `@Username`: Indicates a specific viewer is on an individual user cooldown. Other viewers may still use the command unless a global cooldown is also active.
- **Cooldown Expiration**: Localized date and time when the cooldown expires and the command becomes available again.
- **Manual Reset**: Click **Reset** on any row to immediately clear the cooldown, unlocking the command instantly.
