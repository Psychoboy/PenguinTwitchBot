# Session Monitoring & Disconnect

Managing active connected browser sessions allows administrators to verify overlay uptime and manage dashboard access.

## Filtering Sessions

- Use the search bar in the table toolbar to filter sessions by username, page route, or client IP.
- Quickly inspect if OBS browser sources, remote editors, or stream deck sidecars are maintaining healthy connections.

## Terminating Stale Sessions

- **Forced Disconnect**: Clicking the disconnect button forcefully closes the underlying SignalR circuit for that connection ID.
- **Troubleshooting**: If a browser tab becomes desynchronized or unresponsive, disconnecting forces the client to reconnect and resubscribe to fresh state events.

