### Runtime Feature Services

The bot runtime divides features into two categories:

#### Core Services
Core services are **Always On**. They manage command registration, Twitch message dispatching, database connectivity, and security.

#### Optional Services
Optional services can be toggled on or off without restarting the entire bot. When enabled, startup logic initializes connections and listeners; when disabled, resources are gracefully released.

#### Service Restart
Click **Restart** on any active service to reinitialize its client connection, reload external settings, or recover from transient network errors without restarting the host application.

