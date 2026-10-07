# OBS Connection Troubleshooting

Solutions for common OBS WebSocket connectivity and authentication problems.

---

### "Connection Refused" or Cannot Connect

1. **Verify OBS WebSocket is Running**:
   - In OBS Studio, check *Tools > WebSocket Server Settings*.
   - Verify that **Enable WebSocket server** is checked and the server status shows active.
2. **Check Port Matching**:
   - The default OBS WebSocket port is `4455`. If you customized the port in OBS, verify the same port is in your URL in [OBS Connections](/obs/connections) (e.g. `ws://localhost:4455`).
3. **Firewall / Network on Dual-PC Setups**:
   - If connecting across local machines, ensure Windows Defender Firewall permits incoming connections on port `4455` on the streaming PC.
   - Use the internal IP address (e.g. `ws://192.168.1.150:4455`) instead of `localhost`.

---

### Authentication Errors

- OBS WebSocket v5 requires SHA256 password hashing.
- If you receive authentication failure errors, re-copy the password from *Tools > WebSocket Server Settings > Server Password* into the connection dialog and click **Save**.

---

### Source or Scene Not Found

- If a sub-action reports that a scene or source was not found, check for exact character casing.
- Source and scene names are case-sensitive in OBS Studio.

