# Release Channels & Recovery Bundles

Configure whether to receive pre-release builds and how to recover from an unsuccessful update.

---

### Release Channel Settings

- **Stable Channel (Default)**: Tracks official, verified releases suitable for daily live production streams.
- **Pre-Release Channel (Beta / Preview)**:
  - Toggle **Include pre-releases (Beta / Preview builds)** on to test upcoming features, new sub-action types, or UI redesigns early.
  - Preview versions may contain experimental functionality or require newer database migrations.

---

### Restoring Last Recovery

If an update fails to launch properly or encounters issues on your system:

1. Click **Restore Last Recovery**.
2. The updater extracts the previous backup snapshot taken immediately before the latest update.
3. Restores your application binaries to the previous working version.

> [!TIP]
> The **Restore Last Recovery** button is only enabled when a valid recovery bundle exists on disk.

