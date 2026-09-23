### YouTube Data API v3 Setup

The YouTube Data API is required for the bot's Song Request feature, enabling viewers to search, validate, and queue songs from YouTube in chat.

#### Google Cloud Console Walkthrough
1. Visit the [Google Cloud Console &ndash; YouTube Data API v3](https://console.cloud.google.com/apis/library/youtube.googleapis.com).
2. Create a new project or select an existing project.
3. Click **Enable** to turn on the YouTube Data API v3.
4. Go to **APIs & Services &rarr; Credentials**.
5. Click **+ Create Credentials** &rarr; select **API key**.
6. *(Recommended)* Click **Edit API key**, navigate to **API restrictions**, and restrict the key specifically to **YouTube Data API v3**.
7. Copy the key, paste it into the YouTube settings tab, and click **Test Connection**.

> [!WARNING]
> **Quota Management:**
> Google allocates 10,000 free quota units per day for YouTube Data API v3. A search query costs approximately 100 units, while querying a video directly by ID or link costs only 1 unit.

