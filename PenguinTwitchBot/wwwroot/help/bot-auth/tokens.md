### OAuth Token Lifecycle & Automatic Refresh

Twitch user access tokens expire after approximately 4 hours. The bot automatically manages and refreshes these tokens in the background:

#### Automatic Refresh Mechanics
1. **Encrypted Storage**: The bot securely stores OAuth refresh tokens in its local configuration.
2. **Background Health Timer**: Background timers periodically validate active access tokens with the Twitch Helix API.
3. **Seamless Renewal**: When a token is close to expiry, the bot requests a fresh token without disconnecting active chat sessions or stream listeners.

> [!NOTE]
> **Manual Refresh:**
> If a Twitch connection reports *Disconnected* or *AuthenticationDisconnected*, click **Validate & Refresh Token** or re-authenticate via the **Sign In** buttons.

