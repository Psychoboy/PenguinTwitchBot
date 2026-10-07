# WebSocket Troubleshooting

Diagnostics and solutions for common WebSocket connection and alert delivery issues.

---

### Diagnosing High Drop Counts

If the **Dropped** counter is increasing for a specific connection:
1. **Slow OBS Source**: The browser source in OBS is likely throttling rendering or running on high GPU load.
   - In OBS Studio, open browser source properties and check **Shutdown source when not visible** or enable hardware acceleration under *OBS Settings > Advanced*.
2. **Buffer Capacity Too Low**: If you trigger massive emote walls or high-frequency fireworks, the per-socket capacity may be reached quickly. Increase **Per-websocket queue capacity** to `500` or `1,000`.

---

### Overlays Not Receiving Alerts

If actions trigger in the dashboard but do not appear in OBS:
1. Verify that the overlay browser source is running:
   - Check the **Current Status** table to confirm an active connection exists matching your overlay name.
2. Check topic subscriptions:
   - The connection must be subscribed to `alerts` (or `all`) to receive standard alert messages.
3. Check alert pause state:
   - If alerts were paused via `!pausealerts`, run `!resumealerts` in chat or click **Resume Alerts** on the homepage dashboard.
4. Clear stalled queues:
   - Click **Clear Global Queue** to flush any stale messages, then click **Refresh**.

