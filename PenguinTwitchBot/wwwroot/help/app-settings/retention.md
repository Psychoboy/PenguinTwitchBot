### Data Retention & Cleanup Policies

Configuring retention periods prevents database bloat and disk exhaustion:

* **Chat History (Months)**: Number of months chat logs remain in the database. Older logs are pruned by the scheduled cleanup task. Set to `0` to retain chat messages forever.
* **IP Log Cleanup (Months)**: Retention threshold for viewer IP connection history used for multi-account giveaway checks and security audits.
* **TTS Audio Max Age (Hours)**: Maximum lifetime of cached TTS speech audio files before removal from local disk storage.
* **Clips Cache Max Age (Days)**: Maximum lifetime of downloaded clip video files before cache pruning.

> [!TIP]
> Setting reasonable limits (e.g. 6–12 months for chat history and 1–2 hours for TTS audio) keeps the bot responsive and database backups fast.

